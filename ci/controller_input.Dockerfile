FROM debian:trixie-slim

# Debian 13's Dolphin is the 2503 release. Keep the exact version observable in
# each capture manifest; this image contains test tooling, never game assets.
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
       binutils ca-certificates dolphin-emu ffmpeg fonts-dejavu-core git \
       libgl1-mesa-dri mesa-utils python3 python3-pil \
       x11-utils xauth xdotool xvfb \
    && rm -rf /var/lib/apt/lists/*

ENV LIBGL_ALWAYS_SOFTWARE=1 \
    SDL_AUDIODRIVER=dummy \
    QT_X11_NO_MITSHM=1

WORKDIR /work
