using System;
using System.IO;
using System.Windows.Forms;
using Vlc.DotNet.Forms;

namespace MovieDesktop
{
  class Program : Form
  {
    private VlcControl video;

    [STAThread]
    static void Main(string[] args)
    {
      Application.EnableVisualStyles();
      Application.Run(new Program());
    }

    public Program()
    {
      var vlcPath = new DirectoryInfo("C:\\Program Files\\VideoLAN\\VLC");
      if (!vlcPath.Exists)
      {
        vlcPath = new DirectoryInfo("C:\\Program Files (x86)\\VideoLAN\\VLC");

        if (!vlcPath.Exists)
        {
          throw new EntryPointNotFoundException("VLC not found on your system.");
        }
      }

      // TODO make this variable
      var file = new FileInfo("test/ejmj6Eq.mp4");
      if (!file.Exists)
      {
        Console.Error.WriteLine("Doesn't exist dude");
        throw new FileNotFoundException("Video file doesn't exist!");
      }

      // Forms part
      Text = "Fullscreen desktop movie";
      FormBorderStyle = FormBorderStyle.None;
      WindowState = FormWindowState.Maximized;
      //TopMost = false

      // Video part
      video = new VlcControl
      {
        VlcLibDirectory = vlcPath
      };
      video.EndInit();

      // This ought to be enough for everybody
      video.SetMedia(file, new string[]{"input-repeat=65535"});

      // No audio
      video.Audio.IsMute = true;

      // Error handling
      video.EncounteredError += (sender, e) =>
      {
        Console.Error.Write("An error occurred - " + e);
      };

      // Add to main form
      Controls.Add(video);

      // Watch program resize
      Resize += (sender, e) => {
        video.Width = Width;
        video.Height = Height;
      };

      // Play video
      video.Play();
    }

  }
}
