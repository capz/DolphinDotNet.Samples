#!/usr/bin/env python3
"""Drive real Dolphin 2503 GameCube SI hotplug through its Qt settings dialog.

Only X11 input events and Dolphin's own settings are used. This helper never
reads or writes guest application memory. A caller must separately observe the
result in the guest's PAD snapshots; persisted settings are request evidence,
not proof that libogc has finished detecting the device.

The pinned UI's GamecubeControllersWidget::SaveSettings calls
SerialInterface::ChangeDevice for live ports after a dropdown changes:
https://github.com/dolphin-emu/dolphin/blob/2503/Source/Core/DolphinQt/Config/GamecubeControllersWidget.cpp
"""
from __future__ import annotations

import configparser
from pathlib import Path
import re
import subprocess
import time
from typing import Mapping


class HotplugError(RuntimeError):
    def __init__(self, message: str, evidence: dict | None = None):
        super().__init__(message)
        self.evidence = evidence or {}


class DolphinHotplug:
    """Operate one existing full-Qt Dolphin process, without launching another.

    ``port`` is zero based. Use ``connected=False`` for None and True for
    Standard Controller. The emulator must use English UI, scale factor 1 and
    have its main menu visible. NoGUI Dolphin has no settings dialog and is
    intentionally unsupported.

    ``row_centers`` are client coordinates for the four GC dropdowns in the
    pinned Debian 13 Qt layout. They can be supplied after inspecting a
    different font/DPI layout. A wrong target fails the settings check rather
    than producing hotplug success.
    """

    NONE = 0
    STANDARD_CONTROLLER = 6

    def __init__(self, process, env: Mapping[str, str], user_directory: Path,
                 *, render_window: int | None = None,
                 main_window: int | None = None, timeout: float = 12,
                 row_centers: tuple[int, int, int, int] = (47, 78, 109, 140)):
        self.process = process
        self.env = dict(env)
        self.config_path = Path(user_directory) / "Config" / "Dolphin.ini"
        self.render_window = render_window
        self.main_window = main_window
        self.timeout = timeout
        self.row_centers = row_centers
        self.history: list[dict] = []

    def _alive(self):
        if self.process.poll() is not None:
            raise HotplugError("Dolphin exited during SI hotplug")

    def _x(self, *arguments: object, check: bool = True) -> subprocess.CompletedProcess:
        self._alive()
        return subprocess.run(["xdotool", *map(str, arguments)], env=self.env,
                              capture_output=True, text=True, check=check,
                              timeout=min(self.timeout, 10))

    def _windows(self) -> list[dict]:
        result = self._x("search", "--onlyvisible", "--pid", self.process.pid, check=False)
        if result.returncode not in (0, 1):
            raise HotplugError("Cannot enumerate Dolphin's X11 windows: " + result.stderr.strip())
        windows = []
        for value in result.stdout.split():
            window = int(value)
            name = self._x("getwindowname", window, check=False)
            if name.returncode == 0:
                windows.append({"id": window, "title": name.stdout.strip()})
        return windows

    def _find_main(self) -> int:
        windows = self._windows()
        if self.main_window is not None and any(w["id"] == self.main_window for w in windows):
            return self.main_window
        candidates = [w for w in windows if w["title"].startswith("Dolphin")
                      and w["id"] != self.render_window]
        exact = [w for w in candidates
                 if re.fullmatch(r"Dolphin(?: \[[^]]+\])? [0-9][0-9.\-]*", w["title"])]
        candidates = exact or [w for w in candidates if w["title"] != "Dolphin"]
        if not candidates:
            raise HotplugError("Full Qt Dolphin main window was not found", {"windows": windows})
        self.main_window = min(candidates, key=lambda w: len(w["title"]))["id"]
        return self.main_window

    def _focus(self, window: int):
        self._x("windowraise", window)
        # windowfocus uses XSetInputFocus and works without an EWMH window manager.
        self._x("windowfocus", "--sync", window)

    def _key(self, *keys: str):
        self._x("key", "--clearmodifiers", "--delay", "80", *keys)

    def _dialog(self) -> int | None:
        matches = [w["id"] for w in self._windows() if w["title"] == "Controller Settings"]
        if len(matches) > 1:
            raise HotplugError("Ambiguous Controller Settings windows")
        return matches[0] if matches else None

    def _open_dialog(self) -> int:
        existing = self._dialog()
        if existing is not None:
            self._focus(existing)
            return existing
        self._focus(self._find_main())
        # These accelerators are unique in the English Dolphin 2503 menu.
        self._key("alt+o")
        self._key("c")
        deadline = time.monotonic() + self.timeout
        while time.monotonic() < deadline:
            window = self._dialog()
            if window is not None:
                self._focus(window)
                return window
            time.sleep(.05)
        raise HotplugError("Controller Settings did not open through Options / Controller Settings")

    def _geometry(self, window: int) -> dict:
        result = self._x("getwindowgeometry", "--shell", window)
        values = {}
        for line in result.stdout.splitlines():
            if "=" in line:
                key, value = line.split("=", 1)
                if value.lstrip("-").isdigit():
                    values[key.lower()] = int(value)
        if not {"x", "y", "width", "height"} <= values.keys():
            raise HotplugError("Incomplete Controller Settings geometry", {"geometry": values})
        return values

    def configuration(self) -> list[int]:
        parser = configparser.ConfigParser(interpolation=None, strict=False)
        with self.config_path.open() as stream:
            parser.read_file(stream)
        return [parser.getint("Core", f"SIDevice{port}") for port in range(4)]

    def _restore_render(self, dialog: int):
        self._focus(dialog)
        self._key("Escape")
        deadline = time.monotonic() + self.timeout
        while self._dialog() is not None:
            if time.monotonic() >= deadline:
                raise HotplugError("Controller Settings did not close")
            time.sleep(.05)
        windows = self._windows()
        if self.render_window is None:
            candidates = [w for w in windows if w["id"] != self.main_window
                          and (w["title"].startswith("Dolphin") or "ControllerInput" in w["title"])]
            if candidates:
                self.render_window = max(candidates, key=lambda w: len(w["title"]))["id"]
        if self.render_window is not None and any(w["id"] == self.render_window for w in windows):
            self._focus(self.render_window)

    def set_port(self, port: int, connected: bool) -> dict:
        if port not in range(4) or not isinstance(connected, bool):
            raise ValueError("Expected port 0..3 and a boolean connection request")
        before = self.configuration()
        wanted = self.STANDARD_CONTROLLER if connected else self.NONE
        event = {"port": port, "requested_connected": connected,
                 "method": "Dolphin2503 Qt GC dropdown -> SerialInterface.ChangeDevice",
                 "before_si_devices": before, "requested_si_device": wanted,
                 "guest_state_verified": False}
        if before[port] == wanted:
            event.update(changed=False, after_si_devices=before)
            self.history.append(event)
            return event
        dialog = self._open_dialog()
        geometry = self._geometry(dialog)
        x = max(95, min(geometry["width"] - 110, round(geometry["width"] * .43)))
        y = self.row_centers[port]
        if not 0 < y < geometry["height"]:
            raise HotplugError("GC dropdown coordinate outside settings window", event)
        event.update(dialog_window=dialog, dialog_geometry=geometry,
                     client_click={"x": x, "y": y})
        # Open the real QComboBox popup, select its source-defined first or
        # second item, then commit. No guest state or config file is written here.
        self._x("mousemove", "--window", dialog, x, y)
        self._x("click", "1")
        self._key("Home", *( ("Down",) if connected else () ), "Return")
        expected = before.copy()
        expected[port] = wanted
        deadline = time.monotonic() + self.timeout
        after = before
        while time.monotonic() < deadline:
            try:
                after = self.configuration()
            except (OSError, ValueError, configparser.Error):
                time.sleep(.05)
                continue
            if after == expected:
                break
            if any(after[p] != before[p] for p in range(4) if p != port):
                event["after_si_devices"] = after
                raise HotplugError("GUI hotplug changed another port", event)
            time.sleep(.05)
        else:
            event["after_si_devices"] = after
            raise HotplugError("Dolphin did not persist the requested SI device", event)
        event.update(changed=True, after_si_devices=after)
        self._restore_render(dialog)
        self.history.append(event)
        return event
