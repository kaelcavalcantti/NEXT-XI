set -e
cat part0.mjpeg part1.mjpeg part2.mjpeg > all.mjpeg
ffmpeg -y -v error -f mjpeg -framerate 30 -i all.mjpeg -i audio/mix.wav \
 -vf "eq=contrast=1.14:saturation=1.32:gamma=0.96:brightness=0.01,curves=r='0/0 0.5/0.53 1/1':b='0/0.02 0.5/0.47 1/0.95',noise=alls=7:allf=t,format=yuv420p" \
 -af "acompressor=threshold=-16dB:ratio=2.5:attack=10:release=150,loudnorm=I=-14:TP=-1.2:LRA=9" -ar 48000 \
 -c:v libx264 -preset slow -crf 19 -profile:v high -movflags +faststart -c:a aac -b:a 192k -shortest out.mp4
ffprobe -v error -show_entries format=duration,size -of default=nw=1 out.mp4
