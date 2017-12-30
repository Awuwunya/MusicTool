using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Reflection;

namespace MusicTool {
	class Program {
		static void Main(string[] args) {
			string fin = fread("music.asm").Replace("\t", " ").Replace("  ", " ").Replace("\r", "");
			short addr = 0;
			List<int> l = new List<int>();
			List<Size> sz = new List<Size>();
			if(!Directory.Exists("_temp")) Directory.CreateDirectory("_temp").Attributes = FileAttributes.Directory | FileAttributes.Hidden;
			if(File.Exists("_temp/music")) File.Delete("_temp/music");
			File.Create("_temp/music").Close();
			int max = 0;

			Console.WriteLine("Starting to convert music...");
			Stopwatch sw = new Stopwatch();

			foreach(string _s in fin.Split('\n')) {
				sw.Reset();
				sw.Start();
				if(!string.IsNullOrEmpty(_s) && !_s.StartsWith(";")) {
					bool bin = false, z80 = false;
					string s = _s;

					string type = sget(s, s.IndexOf(" ")).ToUpper(), id = "";
					s = scut(s, s.IndexOf(" ") + 1);

					if(type == "SMPS") {
						switch(sget(s, s.Contains(" ") ? s.IndexOf(" ") : s.Length)) {
							case "bin":
								bin = true;
								break;

							case "asm":
								bin = false;
								break;

							case "bank":
								short z = (short)(new FileInfo("_temp/music").Length & 0x7FFF);
								if(z != 0) { // if address is zero, dont bother padding
									fwrite("_temp/.68k", "\tobj " + z + "\n\tcnop 0,$8000");
									process();

									using(FileStream f = new FileStream("_temp/music", FileMode.Append, FileAccess.Write))
									using(BinaryWriter w = new BinaryWriter(f)) {
										w.Write(File.ReadAllBytes("_temp/.bin"));
										w.Flush();
									}
								}
								goto end;

							default:
								e("Type must be bin or asm! Was '" + sget(s, s.IndexOf(" ")) + "'!");
								return;
						}

						s = scut(s, s.IndexOf(" ") + 1);

					} else {
						id = sget(s, s.IndexOf(" ")).ToUpper();
						s = scut(s, s.IndexOf(" ") + 1);
					}


					string driver = sget(s, s.IndexOf(" ")), file = "", fname = "";
					s = scut(s, s.IndexOf(" ") + 1);

					if(!Directory.Exists(type +'/'+ driver)) {
						e("Folder '" + type + '/' + driver + "' does not exist!");
						return;
					}

					if(type == "SMPS") {
						if(!s.StartsWith("\"")) {
							e("String expected!");
							return;
						}

						s = scut(s, 1);     // remove first quote mark
						file = sget(s, s.IndexOf("\""));
						fname = file.Replace(" ", "").Replace("_", "").Replace("-", "").Replace("'", "");
						file += (bin ? ".bin" : ".asm");

						if(!File.Exists("music\\" + file)) {
							e("Music file '" + file + "' does not exist!");
							return;
						}

						s = scut(s, s.IndexOf("\"") + 2);
					}

					if(!s.StartsWith("\"")) {
						e("String expected! " + s);
						return;
					}
					
					string dvname = sget(s, s.IndexOf("\"", 1) + 1);
					s = scut(s, s.IndexOf("\"", 1) + 2);

					if (!s.StartsWith("\"")) {
						e("String expected! "+ s);
						return;
					}

					string name = sget(s, s.LastIndexOf("\"") +1);
					s = scut(s, s.LastIndexOf("\"") + 1);

					if(type == "SMPS") {
						switch(s.Replace(" ", "")) {
							case "z80":
								z80 = true;
								break;

							case "68k":
								z80 = false;
								break;

							default:
								e("Type must be bin or asm! Was '" + sget(s, s.IndexOf(" ")) + "'!");
								return;
						}
					} else {
						file = name.Replace("\"", "");
					}

					int x = (int)new FileInfo("_temp/music").Length;
					addr = (short)x;
					if(!z80) l.Add(x);

					if(name.Length > max) {
						max = name.Length;
					}

					if(bin) {
						string align = "";
						if(z80) {
							if((int)new FileInfo("music/" + file).Length > 0x8000) {
								Console.WriteLine("WARN: File '" + file + "' will not fit in a single Z80 bank!");
							}

							if((z80addr(addr) & 0xFF8000) != ((z80addr(addr) + (int)new FileInfo("music/" + file).Length) & 0xFF8000)) {
								l.Add((x & 0xFF8000) + 0x8000);
								align = "\n\talign $8000";

							} else {
								l.Add(x);
							}
						}

						fwrite("_temp/.68k", "a section \n\tinclude \"drv.asm\"\n\tinclude \"code/macro.asm\"" + align + "\n\tasc2.w $8000," + name + "\n\tasc2.w $8000,"+ dvname +"\n\tdc.w " + driver + "\n\tincbin \"music/" + file + "\"\n\teven");

					} else if(type == "SMPS") {
						string xf = "a section" + (z80 ? " obj(<obj>)" : "") + "<align>\n\tinclude \"drv.asm\"\n\tinclude \"code/macro.asm\"\n\tinclude \"code/smps2asm.asm\"\n\tinclude \"" + type + '/' + driver + "/smps2asm.asm\"\n\tasc2.w $8000," + name + "\n\tasc2.w $8000," + dvname + "\n\tdc.w " + driver + "\n\topt ae-\n\tinclude \"music/" + file + "\"\n\teven";
						string align = "";
						if(z80) {
							fwrite("_temp/.68k", xf.Replace("<obj>", "" + z80addr(addr)).Replace("<align>", ""));
							process();

							if((int)new FileInfo("_temp/.bin").Length > 0x8000) {
								Console.WriteLine("WARN: File '" + file + "' will not fit in a single Z80 bank!");
							}

							if((z80addr(addr) & 0xFF8000) != ((z80addr(addr) + (int)new FileInfo("_temp/.bin").Length) & 0xFF8000)) {
								l.Add((x & 0xFF8000) + 0x8000);
								align = "\n\talign $8000";

							} else {
								l.Add(x);
							}
						}

						fwrite("_temp/.68k", xf.Replace("<obj>", "" + (z80 ? z80addr(addr) : addr)).Replace("<align>", align));

					} else if(type == "GEMS") {
						fwrite("_temp/.68k", "a section \n\tinclude \"drv.asm\"\n\tinclude \"code/macro.asm\"\n\tasc2.w $8000," + name + "\n\tasc2.w $8000," + dvname + "\n\tdc.w " + driver +','+ id);

					} else {
						Console.WriteLine("Invalid sound driver type '"+ type +"'!");
					}

					process();
					int fsz = (int)new FileInfo("_temp/.bin").Length;
					sz.Add(new Size(name.Replace("\"", ""), fsz));
					sw.Stop();
					Console.WriteLine("Music file '" + file + "' done! Took " + sw.ElapsedMilliseconds + "ms. Size is $" + fsz.ToString("X4") + " bytes.");

					using(FileStream f = new FileStream("_temp/music", FileMode.Append, FileAccess.Write))
					using(BinaryWriter w = new BinaryWriter(f)) {
						w.Write(File.ReadAllBytes("_temp/.bin"));
						w.Flush();
					}
				}

				end:;
			}

			max += 8;
			max += 8 - (max & 7);
			string o = "";
			foreach(int x in l) {
				o += "\tdc.l MusicOff+" + x + "\n";
			}

			o += "musnum = " + (l.Count * 4);

			foreach(Size x in sz) {
				string y = "inform 0,\"" + x.name + ':';
				int av = (max - y.Length);
				int pad = (max - y.Length +1) / 8 + 1;

				for(int i = 0;i < pad;i++) {
					y += '\t';
				}
				o += "\n\t"+ y +'$'+ x.size.ToString("X4") +" bytes\"";
			}
			File.WriteAllText("_temp/offs", o);
		}

		private static int z80addr(short addr) {
			return ((short)(addr | 0x8000) & 0xFFFF);
		}

		private static void process() {
			Process p = new Process();
			p.StartInfo.FileName = "bin/asm68k";
			p.StartInfo.Arguments = "/p /m _temp/.68k, _temp/.bin, , _temp/.lst";
			p.StartInfo.WorkingDirectory = System.IO.Directory.GetCurrentDirectory();
			p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			p.StartInfo.CreateNoWindow = true;
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardOutput = true;
	//		p.StartInfo.RedirectStandardError = true;
			p.Start();

			StreamReader sr = p.StandardOutput;
			string o = sr.ReadToEnd();
			p.WaitForExit();

			if(!File.Exists("_temp/.bin")) {
				e("Uh oh spaghettios!\n" + o);
				Environment.Exit(-1);
			}
		}

		private static void fwrite(string s, string d) {
			File.WriteAllText(s, d);
		}

		private static string fread(string file) {
			return File.ReadAllText(file);
		}

		private static string scut(string s, int i) {
			return s.Substring(i);
		}

		private static string sget(string s, int l) {
			return s.Substring(0, l);
		}

		private static void e(string v) {
			Console.WriteLine(v);
			Console.ReadKey();
		}
	}

	internal class Size {
		public string name { get; }
		public int size { get; }

		public Size(string n, int s){
			name = n;
			size = s;
		}
	}
}
