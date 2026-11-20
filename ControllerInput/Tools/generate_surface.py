#!/usr/bin/env python3
"""Reproduce/check the controller's shared-vertex front topology offline.

Build the managed sample first (no GameCube toolchain is needed):
    dotnet build ControllerInput/ControllerInput.csproj -c Release -p:DndBuildNative=false
Then, with NumPy and SciPy 1.17.0 available:
    python3 ControllerInput/Tools/generate_surface.py --check
    python3 ControllerInput/Tools/generate_surface.py --write

A tiny temporary .NET host invokes the *production* silhouette/inset functions.
It does not construct ControllerModel, allocate GX meshes, or invoke native code.
SciPy is a development-only dependency; the DOL contains only the checked-in
coordinates/indices and evaluates the existing height/normal field at startup.
"""
from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import subprocess
import tempfile

import numpy as np
import scipy
from scipy.spatial import Delaunay

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_ASSEMBLY = ROOT / "ControllerInput/bin/Release/net8.0/ControllerInput.dll"
OUTPUT = ROOT / "ControllerInput/GeometrySurfaceData.cs"
HOST = r'''using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
var path=Path.GetFullPath(args[0]);var directory=Path.GetDirectoryName(path)!;
AssemblyLoadContext.Default.Resolving+=(_,name)=>{
    var candidate=Path.Combine(directory,name.Name+".dll");
    return File.Exists(candidate)?AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate):null;
};
var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
var model=assembly.GetType("ControllerModel",true)!;
var flags=BindingFlags.NonPublic|BindingFlags.Static;
var outline=(Array)model.GetMethod("Silhouette",flags)!.Invoke(null,null)!;
var builder=assembly.GetType("GeometryBuilder",true)!;
var front=(Array)builder.GetMethod("Inset",flags)!.Invoke(null,new object[]{outline,.072f})!;
var points=new float[front.Length][];
for(int i=0;i<front.Length;i++){
    var point=front.GetValue(i)!;var type=point.GetType();
    points[i]=new[]{(float)type.GetField("X")!.GetValue(point)!, (float)type.GetField("Y")!.GetValue(point)!};
}
Console.WriteLine(JsonSerializer.Serialize(points));
'''


def load_boundary(assembly: Path) -> np.ndarray:
    if not assembly.is_file():
        raise SystemExit(f"Build the managed sample first; missing {assembly}")
    with tempfile.TemporaryDirectory(prefix="controller-surface-") as temporary:
        directory = Path(temporary)
        (directory / "Host.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework>'
            '<ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable>'
            '</PropertyGroup></Project>\n'
        )
        (directory / "Program.cs").write_text(HOST)
        result = subprocess.run(
            ["dotnet", "run", "--project", str(directory / "Host.csproj"),
             "-c", "Release", "--verbosity", "quiet", "--", str(assembly)],
            capture_output=True, text=True, check=True,
        )
        lines = [line for line in result.stdout.splitlines() if line.startswith("[[")]
        if len(lines) != 1:
            raise SystemExit("The managed boundary probe did not return one coordinate array")
        return np.array(json.loads(lines[0]), dtype=float)


def cross(a: np.ndarray, b: np.ndarray) -> float:
    return float(a[0] * b[1] - a[1] * b[0])


def contains(outline: np.ndarray, point: np.ndarray) -> bool:
    x, y = point
    inside = False
    for a, b in zip(outline, np.roll(outline, -1, axis=0)):
        if (a[1] > y) != (b[1] > y) and x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0]:
            inside = not inside
    return inside


def boundary_distance(outline: np.ndarray, point: np.ndarray) -> float:
    other = np.roll(outline, -1, axis=0)
    edges = other - outline
    along = np.clip(((point - outline) * edges).sum(1) / (edges * edges).sum(1), 0, 1)
    return float(np.sqrt(((outline + along[:, None] * edges - point) ** 2).sum(1)).min())


def dome(x: float, y: float, cx: float, cy: float, radius: float) -> float:
    q = 1 - ((x - cx) ** 2 + (y - cy) ** 2) / (radius * radius)
    return max(0, q) ** 2


def height(x: float, y: float) -> float:
    # Mirrors the named production height field only for an independent error
    # bound. The boundary and rendered vertices come from production C#.
    return (.075 + .165 * dome(x, y, -1.397, .491, .94)
            + .155 * dome(x, y, 1.34, .47, 1.05)
            + .05 * dome(x, y, -.75, -.54, .63)
            + .05 * dome(x, y, .77, -.54, .63)
            + .04 * dome(x, y, 0, 1.3, 1.55))


def generate(outline: np.ndarray) -> tuple[str, dict]:
    if len(outline) != 128:
        raise SystemExit("Expected the production 128-point bevel boundary")
    points = list(outline)
    interior = []
    for iy in range(-21, 22):
        for ix in range(-30, 31):
            point = np.array([ix * .08, iy * .08])
            if contains(outline, point) and boundary_distance(outline, point) > .027:
                points.append(point)
                interior.append(point)
    points = np.array(points)
    triangles = []
    for triangle in Delaunay(points).simplices:
        if contains(outline, points[triangle].mean(0)):
            a, b, c = points[triangle]
            if cross(b - a, c - a) < 0:
                triangle = triangle[[0, 2, 1]]
            triangles.append(triangle)
    triangles = np.array(triangles)
    edges: dict[tuple[int, int], int] = {}
    for triangle in triangles:
        for a, b in zip(triangle, np.roll(triangle, -1)):
            key = tuple(sorted((int(a), int(b))))
            edges[key] = edges.get(key, 0) + 1
    expected = {tuple(sorted((i, (i + 1) % 128))) for i in range(128)}
    boundary = {edge for edge, count in edges.items() if count == 1}
    if boundary != expected or any(count not in (1, 2) for count in edges.values()):
        raise SystemExit("Topology does not preserve the complete manifold bevel boundary")
    area = sum(abs(cross(points[t[1]] - points[t[0]], points[t[2]] - points[t[0]])) * .5 for t in triangles)
    polygon_area = abs(sum(cross(a, b) for a, b in zip(outline, np.roll(outline, -1, axis=0)))) * .5
    if abs(area - polygon_area) >= 1e-7:
        raise SystemExit("Tessellation changes the reference silhouette area")
    z = np.array([height(*p) for p in points])
    errors = []
    for triangle in triangles:
        for weights in ((1/3, 1/3, 1/3), (.5, .5, 0), (.5, 0, .5), (0, .5, .5)):
            point = (points[triangle] * np.array(weights)[:, None]).sum(0)
            errors.append(float((z[triangle] * weights).sum() - height(*point)))
    if max(errors) >= .004:
        raise SystemExit("Surface interpolation exceeds the 0.004-unit legend standoff")
    source = ('// Offline Delaunay topology of the measured shell outline plus a 0.08-unit\n'
              '// interior grid. Runtime adds the analytic depth and normal once at startup.\n'
              '// The 128 boundary vertices are shared exactly with the bevel profile.\n'
              'internal static class GeometrySurfaceData\n{\n'
              '    internal static readonly float[] Interior={\n')
    for i, point in enumerate(interior):
        source += '        ' + ','.join(f'{value:.6f}f' for value in point) + ',' + ('\n' if i % 4 == 3 else '')
    source += '\n    };\n    internal static readonly ushort[] Triangles={\n'
    for i, triangle in enumerate(triangles):
        source += '        ' + ','.join(str(int(value)) for value in triangle) + ',' + ('\n' if i % 6 == 5 else '')
    source += '\n    };\n}\n'
    report = dict(front_vertices=len(points), front_triangles=len(triangles),
                  boundary_edges=len(boundary), area=area,
                  maximum_analytic_overshoot=max(errors), minimum_error=min(errors),
                  scipy=scipy.__version__)
    return source, report


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--assembly', type=Path, default=DEFAULT_ASSEMBLY)
    parser.add_argument('--check', action='store_true', help='check without writing (default)')
    parser.add_argument('--write', action='store_true', help='replace the checked-in topology after validation')
    args = parser.parse_args()
    if args.check and args.write:
        parser.error('choose --check or --write')
    if scipy.__version__ != '1.17.0':
        parser.error('reproducible topology requires SciPy 1.17.0')
    source, report = generate(load_boundary(args.assembly.resolve()))
    if args.write:
        OUTPUT.write_text(source)
        report['result'] = 'written'
    elif OUTPUT.read_text() != source:
        raise SystemExit('GeometrySurfaceData.cs differs; inspect the source changes before using --write')
    else:
        report['result'] = 'matches checked-in topology'
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
