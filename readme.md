This is work in progress, mostly untested and experimental - please use with caution.

The initial goal is to decompress and compare the different versions of the hobbit soundtrack.
After that maybe look into unpacking and decompressing the different versions of area 51.

### Build
 * Build using Visual Studio
 * Copy mss32.dll from one of the games to the bin directory.

### Usage
Use it from the command line:
```
audiopkg <filename> [args]
flags:
    -e, --extract: extract all audio files from the package
    -d, --decompress: decompress all audio files
    -t, --txth: package the extracted audio for vgmstream using a txth file
    -v, --verbose: print a bunch of information as we're reading the file
```

For decompressing pc files you need to copy mss32.dll from the game's directory to the
same directory as audiopkg.exe.

For playing back the .vgmstream files you can use foobar2000 or audacious with a recent
version of the vgmstream extension.
