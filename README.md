# MovieDesktop
Have videos as desktop backgrounds in Windows. You need to have [VLC](https://www.videolan.org/vlc/) installed (32 bit or 64 bit depending on your system).

## Usage
1. Download the binary zip (32 or 64 bit, depending on which VLC client you have installed) from `./dist/` and unpack, or get the source from `./src/` and compile it in Visual Studio
2. Run from the command line using `MovieDesktop.exe [file, directory or URL of mp4] [optional desktop number]`. If a directory is provided, it will pick a random video file from there.

For example, to open an imgur mp4 as background on your second screen:

    MovieDesktop.exe https://i.imgur.com/VMb5aPE.mp4 2

## Roadmap
* Cross-device testing (ie bootcamp OSX support)
* Adding GUI for opening video

## Disclaimer
It works on Windows 10. If it doesn't, please let me know.

Feel free to add PRs, make your own forks, or to add issues.  
