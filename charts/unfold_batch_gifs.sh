# Put this script under a "root folder" where a subfolder named "pngs" is created.

basedir=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
for f in $basedir/*.gif; do
    [ -f "$f" ] || break
    echo "Got file $f"
    bname="$(basename -- $f .gif)"
    echo "Got basename $bname"
    ffmpeg -vsync vfr -i $f $basedir/pngs/"$bname"_%d.png
done
