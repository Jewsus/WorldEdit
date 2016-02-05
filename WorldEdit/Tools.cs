using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace WorldEdit
{
	public static class Tools
	{
		const int BUFFER_SIZE = 1048576;
		const int MAX_UNDOS = 50;

		public static string GetClipboardPath(string accountName)
		{
			return Path.Combine("worldedit", String.Format("clipboard-{0}.dat", accountName));
		}
		public static List<int> GetColorID(string color)
		{
			int ID;
			if (int.TryParse(color, out ID) && ID >= 0 && ID < Main.numTileColors)
				return new List<int> { ID };

			var list = new List<int>();
			foreach (var kvp in WorldEdit.Colors)
			{
				if (kvp.Key == color)
					return new List<int> { kvp.Value };
				if (kvp.Key.StartsWith(color))
					list.Add(kvp.Value);
			}
			return list;
		}
		public static List<int> GetTileID(string tile)
		{
			int ID;
			if (int.TryParse(tile, out ID) && ID >= 0 && ID < Main.maxTileSets)
				return new List<int> { ID };

			var list = new List<int>();
			foreach (var kvp in WorldEdit.Tiles)
			{
				if (kvp.Key == tile)
					return new List<int> { kvp.Value };
				if (kvp.Key.StartsWith(tile))
					list.Add(kvp.Value);
			}
			return list;
		}
		public static List<int> GetWallID(string wall)
		{
			int ID;
			if (int.TryParse(wall, out ID) && ID >= 0 && ID < Main.maxWallTypes)
				return new List<int> { ID };

			var list = new List<int>();
			foreach (var kvp in WorldEdit.Walls)
			{
				if (kvp.Key == wall)
					return new List<int> { kvp.Value };
				if (kvp.Key.StartsWith(wall))
					list.Add(kvp.Value);
			}
			return list;
		}
		public static bool HasClipboard(string accountName)
		{
			return File.Exists(Path.Combine("worldedit", String.Format("clipboard-{0}.dat", accountName)));
		}
		public static Tile[,] LoadWorldData(string path)
		{
			Tile[,] tile;
			// GZipStream is already buffered, but it's much faster to have a 1 MB buffer.
			using (var reader =
				new BinaryReader(
					new BufferedStream(
						new GZipStream(File.Open(path, FileMode.Open), CompressionMode.Decompress), BUFFER_SIZE)))
			{
				reader.ReadInt32();
				reader.ReadInt32();
				int width = reader.ReadInt32();
				int height = reader.ReadInt32();
				tile = new Tile[width, height];

				for (int i = 0; i < width; i++)
				{
					for (int j = 0; j < height; j++)
						tile[i, j] = ReadTile(reader);
				}
				return tile;
			}
		}
		public static void LoadWorldSection(string path)
		{
            // GZipStream is already buffered, but it's much faster to have a 1 MB buffer.
            using (var reader =
                new BinaryReader(
                    new BufferedStream(
                        new GZipStream(File.Open(path, FileMode.Open), CompressionMode.Decompress), BUFFER_SIZE)))
            {
                int x = reader.ReadInt32();
                int y = reader.ReadInt32();
                int width = reader.ReadInt32();
                int height = reader.ReadInt32();

                for (int i = x; i < x + width; i++)
                {
                    for (int j = y; j < y + height; j++)
                    {
                        Main.tile[i, j] = reader.ReadTile();
                        Main.tile[i, j].skipLiquid(true);
                    }
                    ResetSection(x, y, x + width, y + height);
                }
            }
        }
		public static void PrepareUndo(int x, int y, int x2, int y2, TSPlayer plr)
		{
			if (WorldEdit.Database.GetSqlType() == SqlType.Mysql)
				WorldEdit.Database.Query("INSERT IGNORE INTO WorldEdit VALUES (@0, -1, -1)", plr.User.Name);
			else
				WorldEdit.Database.Query("INSERT OR IGNORE INTO WorldEdit VALUES (@0, 0, 0)", plr.User.Name);
			WorldEdit.Database.Query("UPDATE WorldEdit SET RedoLevel = -1 WHERE Account = @0", plr.User.Name);
			WorldEdit.Database.Query("UPDATE WorldEdit SET UndoLevel = UndoLevel + 1 WHERE Account = @0", plr.User.Name);

			int undoLevel = 0;
			using (var reader = WorldEdit.Database.QueryReader("SELECT UndoLevel FROM WorldEdit WHERE Account = @0", plr.User.Name))
			{
				if (reader.Read())
					undoLevel = reader.Get<int>("UndoLevel");
			}

			string path = Path.Combine("worldedit", String.Format("undo-{0}-{1}.dat", plr.User.Name, undoLevel));
			SaveWorldSection(x, y, x2, y2, path);

			foreach (string fileName in Directory.EnumerateFiles("worldedit", String.Format("redo-{0}-*.dat", plr.User.Name)))
				File.Delete(fileName);
			File.Delete(Path.Combine("worldedit", String.Format("undo-{0}-{1}.dat", plr.User.Name, undoLevel - MAX_UNDOS)));
		}
		public static Tile ReadTile(this BinaryReader reader)
		{
			Tile tile = new Tile();
			tile.sTileHeader = reader.ReadInt16();
			tile.bTileHeader = reader.ReadByte();
			tile.bTileHeader2 = reader.ReadByte();

			// Tile type
			if (tile.active())
			{
				tile.type = reader.ReadUInt16();
				if (Main.tileFrameImportant[tile.type])
				{
					tile.frameX = reader.ReadInt16();
					tile.frameY = reader.ReadInt16();
				}
			}
			tile.wall = reader.ReadByte();
			tile.liquid = reader.ReadByte();
			return tile;
		}

        public static void ReadChests(this BinaryReader reader, int x, int y)
        {
            int num = (int)reader.ReadInt32();
            int num2 = (int)reader.ReadInt16();
            TSPlayer.All.SendInfoMessage("Total Chests: {0}, Total Slots: {1}", num, num2);
            int num3;
            int num4;
            if (num2 < 40)
            {
                num3 = num2;
                num4 = 0;
            }
            else
            {
                num3 = 40;
                num4 = num2 - 40;
            }
            int i;
            for (i = 0; i < num; i++)
            {
                for (int c = 0; c < 1000; c++)
                {
                    if (Main.chest[c] == null)
                    {
                        Chest chest = new Chest(false);
                        chest.x = reader.ReadInt32() + x;
                        chest.y = reader.ReadInt32() + y;
                        chest.name = reader.ReadString();
                        TSPlayer.All.SendInfoMessage("Load: {0}x{1} Offset: {2}x{3}", chest.x, chest.y, chest.x - x, chest.y - y);
                        for (int j = 0; j < num3; j++)
                        {
                            short num5 = reader.ReadInt16();
                            Item item = new Item();
                            if (num5 > 0)
                            {
                                item.netDefaults(reader.ReadInt16());
                                item.stack = (int)num5;
                                item.Prefix((int)reader.ReadByte());
                            }
                            else if (num5 < 0)
                            {
                                item.netDefaults(reader.ReadInt16());
                                item.Prefix((int)reader.ReadByte());
                                item.stack = 1;
                            }
                            chest.item[j] = item;
                        }
                        for (int j = 0; j < num4; j++)
                        {
                            short num5 = reader.ReadInt16();
                            if (num5 > 0)
                            {
                                reader.ReadInt16();
                                reader.ReadByte();
                            }
                        }
                        Main.chest[i] = chest;
                        break;
                    }
                }
            }
            TSPlayer.All.SendInfoMessage("{0} Chests Loaded.", num);
        }

        public static bool Redo(string accountName)
		{
			int redoLevel = 0;
			int undoLevel = 0;
			using (var reader = WorldEdit.Database.QueryReader("SELECT RedoLevel, UndoLevel FROM WorldEdit WHERE Account = @0", accountName))
			{
				if (reader.Read())
				{
					redoLevel = reader.Get<int>("RedoLevel") - 1;
					undoLevel = reader.Get<int>("UndoLevel") + 1;
				}
				else
					return false;
			}

			if (redoLevel < -1)
				return false;

			string redoPath = Path.Combine("worldedit", String.Format("redo-{0}-{1}.dat", accountName, redoLevel + 1));
			WorldEdit.Database.Query("UPDATE WorldEdit SET RedoLevel = @0 WHERE Account = @1", redoLevel, accountName);

			if (!File.Exists(redoPath))
				return false;

			string undoPath = Path.Combine("worldedit", String.Format("undo-{0}-{1}.dat", accountName, undoLevel));
			WorldEdit.Database.Query("UPDATE WorldEdit SET UndoLevel = @0 WHERE Account = @1", undoLevel, accountName);

			using (var reader = new BinaryReader(new GZipStream(new FileStream(redoPath, FileMode.Open), CompressionMode.Decompress)))
			{
				int x = Math.Max(0, reader.ReadInt32());
				int y = Math.Max(0, reader.ReadInt32());
				int x2 = Math.Min(x + reader.ReadInt32() - 1, Main.maxTilesX - 1);
				int y2 = Math.Min(y + reader.ReadInt32() - 1, Main.maxTilesY - 1);
				SaveWorldSection(x, y, x2, y2, undoPath);
			}
			LoadWorldSection(redoPath);
			File.Delete(redoPath);
			return true;
		}
		public static void ResetSection(int x, int y, int x2, int y2)
		{
			int lowX = Netplay.GetSectionX(x);
			int highX = Netplay.GetSectionX(x2);
			int lowY = Netplay.GetSectionY(y);
			int highY = Netplay.GetSectionY(y2);
			foreach (RemoteClient sock in Netplay.Clients.Where(s => s.IsActive))
			{
				for (int i = lowX; i <= highX; i++)
				{
					for (int j = lowY; j <= highY; j++)
						sock.TileSections[i, j] = false;
				}
			}
		}
        public static void SaveWorldSection(int x, int y, int x2, int y2, string path)
        {
            // GZipStream is already buffered, but it's much faster to have a 1 MB buffer.
            using (var writer =
                new BinaryWriter(
                    new BufferedStream(
                        new GZipStream(File.Open(path, FileMode.Create), CompressionMode.Compress), BUFFER_SIZE)))
            {
                writer.Write(x);
                writer.Write(y);
                writer.Write(x2 - x + 1);
                writer.Write(y2 - y + 1);

                for (int i = x; i <= x2; i++)
                {
                    for (int j = y; j <= y2; j++)
                    {
                        writer.Write(Main.tile[i, j] ?? new Tile());
                    }
                }
            }
        }

		public static void Write(this BinaryWriter writer, Tile tile)
		{
			writer.Write(tile.sTileHeader);
			writer.Write(tile.bTileHeader);
			writer.Write(tile.bTileHeader2);
			
			if (tile.active())
			{
				writer.Write(tile.type);
				if (Main.tileFrameImportant[tile.type])
				{
					writer.Write(tile.frameX);
					writer.Write(tile.frameY);
				}
            }
			writer.Write(tile.wall);
			writer.Write(tile.liquid);
		}

        public static void WriteChests(this BinaryWriter writer, int x, int y, int x2, int y2)
        {
            int amount = 0;
            for (int i = 0; i < 1000; i++)
            {
                Chest chest = Main.chest[i];
                if (chest != null)
                {
                    if (new Rectangle(x, y, x2 - x + 1, y2 - y + 1).Contains(chest.x, chest.y))
                    {
                        amount++;
                    }
                }
            }
            writer.Write(amount);
            writer.Write((short)40);
            for (int i = 0; i < 1000; i++)
            {
                Chest chest = Main.chest[i];
                if (chest != null)
                {
                    if (new Rectangle(x, y, x2 - x + 1, y2 - y + 1).Contains(chest.x, chest.y))
                    {
                        writer.Write(chest.x - x);
                        writer.Write(chest.y - y);
                        writer.Write(chest.name);
                        TSPlayer.All.SendInfoMessage("Save: {0}x{1} Offset: {2}x{3}", chest.x, chest.y, chest.x - x, chest.y - y);
                        for (int l = 0; l < 40; l++)
                        {
                            Item item = chest.item[l];
                            if (item == null)
                            {
                                writer.Write(0);
                            }
                            else
                            {
                                if (item.stack > item.maxStack)
                                {
                                    item.stack = item.maxStack;
                                }
                                if (item.stack < 0)
                                {
                                    item.stack = 1;
                                }
                                writer.Write((short)item.stack);
                                if (item.stack > 0)
                                {
                                    writer.Write(item.netID);
                                    writer.Write(item.prefix);
                                }
                            }
                        }
                    }
                }
            }
            TSPlayer.All.SendInfoMessage("{0} Chests Saved.", amount);
        }

        public static bool Undo(string accountName)
		{
			int redoLevel = 0;
			int undoLevel = 0;
			using (var reader = WorldEdit.Database.QueryReader("SELECT RedoLevel, UndoLevel FROM WorldEdit WHERE Account = @0", accountName))
			{
				if (reader.Read())
				{
					redoLevel = reader.Get<int>("RedoLevel") + 1;
					undoLevel = reader.Get<int>("UndoLevel") - 1;
				}
				else
					return false;
			}

			if (undoLevel < -1)
				return false;

			string undoPath = Path.Combine("worldedit", String.Format("undo-{0}-{1}.dat", accountName, undoLevel + 1));
			WorldEdit.Database.Query("UPDATE WorldEdit SET UndoLevel = @0 WHERE Account = @1", undoLevel, accountName);

			if (!File.Exists(undoPath))
				return false;

			string redoPath = Path.Combine("worldedit", String.Format("redo-{0}-{1}.dat", accountName, redoLevel));
			WorldEdit.Database.Query("UPDATE WorldEdit SET RedoLevel = @0 WHERE Account = @1", redoLevel, accountName);

			using (var reader = new BinaryReader(new GZipStream(new FileStream(undoPath, FileMode.Open), CompressionMode.Decompress)))
			{
				int x = Math.Max(0, reader.ReadInt32());
				int y = Math.Max(0, reader.ReadInt32());
				int x2 = Math.Min(x + reader.ReadInt32() - 1, Main.maxTilesX - 1);
				int y2 = Math.Min(y + reader.ReadInt32() - 1, Main.maxTilesY - 1);
				SaveWorldSection(x, y, x2, y2, redoPath);
			}
			LoadWorldSection(undoPath);
			File.Delete(undoPath);
			return true;
		}
	}
}