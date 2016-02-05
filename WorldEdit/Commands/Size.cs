using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace WorldEdit.Commands
{
    public class Size : WECommand
    {
        private bool selection;

        public Size(int x, int y, int x2, int y2, TSPlayer plr, bool selection)
			: base(x, y, x2, y2, plr)
        {
            this.selection = selection;
        }

        public override void Execute()
        {
            int width = 0;
            int height = 0;
            if (!selection)
            {
                string clipboardPath = Tools.GetClipboardPath(plr.User.Name);
                using (var reader = new BinaryReader(new GZipStream(new FileStream(clipboardPath, FileMode.Open), CompressionMode.Decompress)))
                {
                    reader.ReadInt32();
                    reader.ReadInt32();

                    width = reader.ReadInt32();
                    height = reader.ReadInt32();
                }
            }
            else
            {
                width = Math.Abs(x - x2) + 1;
                height = Math.Abs(y - y2) + 1;
            }
            plr.SendSuccessMessage("The {0} size is {1}x{2}.", selection ? "selection" : "clipboard", width, height);
        }
    }
}
