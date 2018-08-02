# MovieDesktop
Allow videos as desktop backgrounds in Windows. You need to have VLC installed (32 bit or 64 bit depending on your system).

## Usage
1. Download the binary zip (32 or 64 bit, depending on which VLC client you have installed) from `./dist/` and unpack, or get the source from `./src/` and compile it in Visual Studio
2. Run from the command line using `MovieDesktop.exe [file, directory or URL of mp4] [optional desktop number]`. If a directory is provided, it will pick a random movie file from there.

For example, to open an imgur mp4 as background on your second screen:

    MovieDesktop.exe https://i.imgur.com/VMb5aPE.mp4 2

## Roadmap
* Cross-device testing (ie solving Windows on Macbook issue)
* Adding GUI for opening video

## Disclaimer
It works on my machine! I cannot guarantee that it works anywhere else but it kind of should.

Feel free to add PRs, make your own forks, or to add issues.  
