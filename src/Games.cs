// LNK Screen Manager - HDR automatique pour les jeux.
// By LNK Co (c) 2026
//
// - Jeux detectes : bibliotheques Steam, manifestes Epic, jeux reconnus par Windows (Game Bar), ajouts manuels.
// - Info HDR : PCGamingWiki (identifiant Steam -> page -> champ "hdr"), une seule fois par jeu, mise en cache.
// - Surveillance : notification Windows "fenetre au premier plan" (aucun sondage, 0 % CPU au repos),
//   puis attente de la fin du processus sur son handle ; le HDR est remis comme avant a la fermeture.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LnkScreenManager
{
    public class Game
    {
        public string Key, Name, Source, Folder, Exe, SteamId, EpicApp;
        public string Wiki;      // valeur PCGamingWiki : true / limited / false / unknown... ; null = pas encore cherchee
        public int User = -1;    // -1 = suivre PCGamingWiki, 0 = non, 1 = oui
        public bool Manual;

        public bool WikiHdr { get { return Wiki == "true" || Wiki == "limited" || Wiki == "always on"; } }
        public bool AutoHdr { get { return User >= 0 ? User == 1 : WikiHdr; } }

        public bool Matches(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (Exe != null && string.Equals(path, Exe, StringComparison.OrdinalIgnoreCase)) return true;
            // jeu ajoute a la main : reconnu aussi par le nom du programme, ou qu'il soit
            // (les lanceurs mettent souvent le jeu dans un dossier qui change a chaque mise a jour : ...\v3.1.1\bin\jeu.exe)
            if (Manual && Exe != null && !Games.GenericExe(Exe) && string.Equals(Path.GetFileName(path), Path.GetFileName(Exe), StringComparison.OrdinalIgnoreCase)) return true;
            return Folder != null && path.StartsWith(Folder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }

        public string WikiLabel
        {
            get
            {
                if (Wiki == null) return L.T(Settings.WikiLookup ? "info HDR en attente" : "pas d'info HDR automatique");
                if (Wiki == "true" || Wiki == "always on") return L.T("HDR natif");
                if (Wiki == "limited") return L.T("HDR (limité)");
                if (Wiki == "false" || Wiki == "hackable" || Wiki == "n/a") return L.T("pas de HDR");
                return L.T("HDR inconnu");
            }
        }
    }

    public static class Games
    {
        static readonly object sync = new object();
        static List<Game> list;
        static string UA { get { return "LNK-Screen-Manager/" + Brand.Version + " (+HDR auto)"; } }
        public static string LastError;
        static string FilePath { get { return AppData.File("games.txt"); } }

        public static List<Game> All { get { lock (sync) { if (list == null) list = Detect(); return list; } } }
        public static void Reload() { lock (sync) list = Detect(); }

        // ------------------------------------------------ detection
        static List<Game> Detect()
        {
            var saved = LoadSaved();
            var res = new List<Game>();
            foreach (var g in Steam().Concat(Epic()).Concat(Gog()).Concat(Xbox()).Concat(Ubisoft()).Concat(EaLegacy()).Concat(FromUninstall()))
                if (!res.Any(x => x.Key == g.Key || SameFolder(x, g))) res.Add(g);
            foreach (var g in WindowsKnown()) if (!res.Any(x => x.Key == g.Key || x.Matches(g.Exe))) res.Add(g);
            foreach (var s in saved.Values.Where(s => s.Manual && File.Exists(s.Exe)))
                if (!res.Any(x => x.Key == s.Key)) res.Add(s);
            foreach (var g in res)
            {
                Game s;
                if (saved.TryGetValue(g.Key, out s)) { g.User = s.User; g.Wiki = s.Wiki; g.Manual = s.Manual || g.Manual; }
            }
            try
            {
                HdrLog.Write(string.Format("Jeux détectés : {0} ({1}). Dossiers Xbox : {2}.", res.Count,
                    string.Join(", ", res.GroupBy(g => g.Source).OrderByDescending(x => x.Count()).Select(x => x.Key + " " + x.Count())),
                    string.Join(", ", XboxRoots().Where(Directory.Exists).DefaultIfEmpty("aucun"))));
            }
            catch { }
            return res.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        static List<Game> Steam()
        {
            var res = new List<Game>();
            try
            {
                var steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                if (string.IsNullOrEmpty(steam)) return res;
                var libs = new List<string> { Norm(steam) };
                string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        libs.Add(Norm(m.Groups[1].Value.Replace("\\\\", "\\")));
                foreach (var lib in libs.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    string apps = Path.Combine(lib, "steamapps");
                    if (!Directory.Exists(apps)) continue;
                    foreach (var acf in Directory.GetFiles(apps, "appmanifest_*.acf"))
                    {
                        string t = File.ReadAllText(acf);
                        string id = Field(t, "appid"), name = Field(t, "name"), dir = Field(t, "installdir");
                        if (id == null || dir == null || IsTool(name)) continue;
                        string folder = Path.Combine(apps, "common", dir);
                        if (Directory.Exists(folder)) res.Add(new Game { Key = "steam:" + id, Name = name ?? dir, Source = "Steam", Folder = folder, SteamId = id });
                    }
                }
            }
            catch { }
            return res;
        }

        // ------------------------------------------------ autres boutiques
        static readonly string[] Special =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        };

        /// Dossier de jeu utilisable : il existe et il est assez precis. Refuse les racines (C:\Jeux) et les dossiers
        /// systeme (Program Files, profil...) : sinon tous les programmes qu'ils contiennent seraient pris pour le jeu.
        public static string SafeFolder(string f)
        {
            if (string.IsNullOrWhiteSpace(f)) return null;
            try
            {
                f = Norm(f.Trim().Trim('"'));
                if (!Directory.Exists(f)) return null;
                if (f.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Length < 3) return null;
                foreach (var s0 in Special)
                {
                    if (string.IsNullOrEmpty(s0)) continue;
                    string s = s0.TrimEnd('\\');
                    if (string.Equals(s, f, StringComparison.OrdinalIgnoreCase) || s.StartsWith(f + "\\", StringComparison.OrdinalIgnoreCase)) return null;
                }
                return f;
            }
            catch { return null; }
        }

        static bool SameFolder(Game a, Game b)
        {
            if (a.Folder == null || b.Folder == null) return false;
            string x = a.Folder.TrimEnd('\\'), y = b.Folder.TrimEnd('\\');
            return string.Equals(x, y, StringComparison.OrdinalIgnoreCase)
                || x.StartsWith(y + "\\", StringComparison.OrdinalIgnoreCase) || y.StartsWith(x + "\\", StringComparison.OrdinalIgnoreCase);
        }

        static List<Game> Gog()
        {
            var res = new List<Game>();
            foreach (var root in new[] { @"SOFTWARE\WOW6432Node\GOG.com\Games", @"SOFTWARE\GOG.com\Games" }) ReadGog(Registry.LocalMachine, root, res);
            return res;
        }

        /// Jeux GOG (cle de registre ecrite par GOG Galaxy et les installateurs GOG).
        public static void ReadGog(RegistryKey hive, string root, List<Game> res)
        {
            try
            {
                using (var k = hive.OpenSubKey(root))
                {
                    if (k == null) return;
                    foreach (var id in k.GetSubKeyNames())
                        using (var c = k.OpenSubKey(id))
                        {
                            if (c == null) continue;
                            string folder = SafeFolder(c.GetValue("path") as string), name = c.GetValue("gameName") as string;
                            if (folder == null || res.Any(x => x.Key == "gog:" + id)) continue;
                            res.Add(new Game { Key = "gog:" + id, Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(folder) : name.Trim(), Source = "GOG", Folder = folder });
                        }
                }
            }
            catch { }
        }

        /// Jeux Xbox / PC Game Pass (fichier MicrosoftGame.config dans le dossier de chaque jeu).
        public static List<Game> Xbox() { return Xbox(XboxRoots()); }

        /// Dossiers de jeux Xbox. L'appli Xbox laisse le choix du dossier sur chaque disque et l'enregistre
        /// dans le fichier cache « .GamingRoot » a la racine du disque : en-tete "RGBX", un entier, puis
        /// les chemins (relatifs au disque) en UTF-16 termines par un zero. XboxGames est le dossier par defaut.
        public static List<string> XboxRoots()
        {
            var res = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady) continue;
                    string root = d.RootDirectory.FullName;
                    res.Add(Path.Combine(root, "XboxGames"));
                    string f = Path.Combine(root, ".GamingRoot");
                    if (!File.Exists(f)) continue;
                    byte[] b = File.ReadAllBytes(f);
                    if (b.Length < 10 || b.Length > 65536 || b[0] != 'R' || b[1] != 'G' || b[2] != 'B' || b[3] != 'X') continue;
                    foreach (var part in Encoding.Unicode.GetString(b, 8, (b.Length - 8) & ~1).Split('\0'))
                    {
                        string rel = part.Trim();
                        if (rel.Length == 0 || rel.IndexOfAny(Path.GetInvalidPathChars()) >= 0) continue;
                        res.Add(Path.Combine(root, rel.TrimStart('\\', '/')));
                    }
                }
                catch { }
            }
            return res.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<Game> Xbox(IEnumerable<string> roots)
        {
            var res = new List<Game>();
            foreach (var root in roots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var dir in Directory.GetDirectories(root))
                    {
                        string content = Path.Combine(dir, "Content"), cfg = Path.Combine(content, "MicrosoftGame.config");
                        if (!File.Exists(cfg)) continue;
                        var dn = Regex.Match(File.ReadAllText(cfg), "DefaultDisplayName\\s*=\\s*\"([^\"]+)\"");
                        string name = dn.Success && !dn.Groups[1].Value.StartsWith("ms-resource", StringComparison.OrdinalIgnoreCase) ? dn.Groups[1].Value : Path.GetFileName(dir);
                        res.Add(new Game { Key = "xbox:" + Path.GetFileName(dir).ToLowerInvariant(), Name = name.Trim(), Source = "Xbox", Folder = content });
                    }
                }
                catch { }
            }
            return res;
        }

        /// Jeux Ubisoft Connect.
        static List<Game> Ubisoft()
        {
            var res = new List<Game>();
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs"))
                {
                    if (k == null) return res;
                    foreach (var id in k.GetSubKeyNames())
                        using (var c = k.OpenSubKey(id))
                        {
                            string folder = SafeFolder(c == null ? null : c.GetValue("InstallDir") as string);
                            if (folder == null) continue;
                            res.Add(new Game { Key = "ubi:" + id, Name = UninstallName("Uplay Install " + id) ?? Path.GetFileName(folder), Source = "Ubisoft", Folder = folder });
                        }
                }
            }
            catch { }
            return res;
        }

        /// Anciens jeux EA / Origin (cle "EA Games\<jeu>" avec "Install Dir").
        static List<Game> EaLegacy()
        {
            var res = new List<Game>();
            foreach (var root in new[] { @"SOFTWARE\WOW6432Node\EA Games", @"SOFTWARE\EA Games" })
            {
                try
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(root))
                    {
                        if (k == null) continue;
                        foreach (var n in k.GetSubKeyNames())
                            using (var c = k.OpenSubKey(n))
                            {
                                string folder = SafeFolder(c == null ? null : c.GetValue("Install Dir") as string);
                                if (folder == null || res.Any(x => x.Key == "ea:" + n.ToLowerInvariant())) continue;
                                res.Add(new Game { Key = "ea:" + n.ToLowerInvariant(), Name = n, Source = "EA", Folder = folder });
                            }
                    }
                }
                catch { }
            }
            return res;
        }

        static readonly string[] UninstallRoots = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };

        static string UninstallName(string keyName)
        {
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
                foreach (var root in UninstallRoots)
                    try { using (var k = hive.OpenSubKey(root + "\\" + keyName)) { var n = k == null ? null : k.GetValue("DisplayName") as string; if (!string.IsNullOrWhiteSpace(n)) return n.Trim(); } }
                    catch { }
            return null;
        }

        static List<Game> FromUninstall()
        {
            var res = new List<Game>();
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
                foreach (var root in UninstallRoots) ReadUninstall(hive, root, res);
            return res;
        }

        /// Jeux installes par EA app, Battle.net, Amazon Games ou Riot (reperes par leur entree « Programmes installes »).
        public static void ReadUninstall(RegistryKey hive, string root, List<Game> res)
        {
            try
            {
                using (var k = hive.OpenSubKey(root))
                {
                    if (k == null) return;
                    foreach (var id in k.GetSubKeyNames())
                        using (var c = k.OpenSubKey(id))
                        {
                            if (c == null) continue;
                            string name = c.GetValue("DisplayName") as string;
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            string src = LauncherOf(name, (c.GetValue("UninstallString") as string) ?? "", (c.GetValue("Publisher") as string) ?? "");
                            if (src == null) continue;
                            string folder = SafeFolder(c.GetValue("InstallLocation") as string);
                            if (folder == null) continue;
                            string key = src.ToLowerInvariant().Replace(".", "") + ":" + id.ToLowerInvariant();
                            if (!res.Any(x => x.Key == key)) res.Add(new Game { Key = key, Name = name.Trim(), Source = src, Folder = folder });
                        }
                }
            }
            catch { }
        }

        static string LauncherOf(string name, string uninst, string pub)
        {
            string u = uninst.ToLowerInvariant(), n = name.Trim().ToLowerInvariant();
            if (u.Contains("amazon game remover")) return "Amazon";
            if (u.Contains("battle.net") && n != "battle.net") return "Battle.net";
            if (u.Contains("riotclientservices") && !n.StartsWith("riot client")) return "Riot";
            bool ea = u.Contains("eainstaller") || u.Contains("\\ea desktop\\") || u.Contains("\\origin\\") || pub.IndexOf("Electronic Arts", StringComparison.OrdinalIgnoreCase) >= 0;
            if (ea && !(n.StartsWith("ea app") || n.StartsWith("ea desktop") || n.StartsWith("ea core") || n == "origin")) return "EA";
            return null;
        }

        static List<Game> Epic()
        {
            var res = new List<Game>();
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");
                if (!Directory.Exists(dir)) return res;
                foreach (var f in Directory.GetFiles(dir, "*.item"))
                {
                    string t = File.ReadAllText(f);
                    string name = Json(t, "DisplayName"), loc = Json(t, "InstallLocation"), app = Json(t, "AppName");
                    if (app == null || loc == null || !Directory.Exists(loc)) continue;
                    res.Add(new Game { Key = "epic:" + app, Name = name ?? app, Source = "Epic", Folder = loc, EpicApp = app });
                }
            }
            catch { }
            return res;
        }

        /// Jeux reconnus par Windows (Game Bar / mode Jeu).
        static List<Game> WindowsKnown()
        {
            var res = new List<Game>();
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Children"))
                {
                    if (k == null) return res;
                    foreach (var n in k.GetSubKeyNames())
                        using (var c = k.OpenSubKey(n))
                        {
                            var p = c == null ? null : c.GetValue("MatchedExeFullPath") as string;
                            if (string.IsNullOrEmpty(p) || IsNoise(p) || !File.Exists(p)) continue;
                            res.Add(new Game { Key = "exe:" + p.ToLowerInvariant(), Name = NameOf(p), Source = "Windows", Exe = p });
                        }
                }
            }
            catch { }
            return res;
        }

        public static string NameOf(string exe)
        {
            // description du fichier, puis nom du produit, puis nom de l'exe (en evitant les noms generiques)
            try
            {
                var v = FileVersionInfo.GetVersionInfo(exe);
                foreach (var s in new[] { v.FileDescription, v.ProductName })
                    if (!string.IsNullOrWhiteSpace(s) && s.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) < 0 && !s.Trim().Equals("Launcher", StringComparison.OrdinalIgnoreCase))
                        return s.Trim();
            }
            catch { }
            return Path.GetFileNameWithoutExtension(exe);
        }

        static string Norm(string p) { try { return Path.GetFullPath(p.Replace('/', '\\')).TrimEnd('\\'); } catch { return p; } }
        static string Field(string t, string k) { var m = Regex.Match(t, "\"" + k + "\"\\s+\"([^\"]*)\""); return m.Success ? m.Groups[1].Value : null; }
        static string Json(string t, string k) { var m = Regex.Match(t, "\"" + k + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); return m.Success ? Regex.Unescape(m.Groups[1].Value) : null; }

        static bool IsTool(string n)
        {
            if (n == null) return false;
            string l = n.ToLowerInvariant();
            return l.Contains("redistributable") || l.Contains("proton") || l.Contains("steamvr") || l.Contains("steam linux runtime") || l.Contains("dedicated server") || l.EndsWith(" sdk");
        }

        static bool IsNoise(string p)
        {
            string l = p.ToLowerInvariant();
            return l.EndsWith("\\java.exe") || l.EndsWith("\\javaw.exe") || l.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows).ToLowerInvariant() + "\\");
        }

        // ------------------------------------------------ reglages enregistres (games.txt)
        static Dictionary<string, Game> LoadSaved()
        {
            var d = new Dictionary<string, Game>();
            try
            {
                if (File.Exists(FilePath))
                    foreach (var l in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        var p = l.Split('|');
                        if (p.Length < 6 || p[0] == "") continue;
                        int u; int.TryParse(p[1], out u);
                        d[p[0]] = new Game { Key = p[0], User = u, Wiki = p[2] == "" ? null : p[2], Manual = p[3] == "1", Exe = p[4] == "" ? null : p[4], Name = p[5], Source = "Ajouté" };
                    }
            }
            catch { }
            return d;
        }

        public static void Save()
        {
            lock (sync)
            {
                if (list == null) return;
                var lines = list.Where(g => g.User >= 0 || g.Wiki != null || g.Manual).Select(g => string.Join("|", g.Key, g.User.ToString(), g.Wiki ?? "", g.Manual ? "1" : "0", g.Manual ? g.Exe : "", g.Manual ? g.Name.Replace("|", " ") : ""));
                try { File.WriteAllLines(FilePath, lines, Encoding.UTF8); } catch { }
            }
        }

        /// Nom de programme trop courant pour reconnaitre un jeu par son seul nom (game.exe, launcher.exe...).
        public static bool GenericExe(string exe)
        {
            string n = Path.GetFileNameWithoutExtension(exe ?? "").ToLowerInvariant();
            return n.Length < 3 || n == "game" || n == "games" || n == "launcher" || n == "client" || n == "start" || n == "play" || n == "main"
                || n == "app" || n == "application" || n == "run" || n == "bin" || n == "win64" || n == "win32" || n == "x64" || n == "shipping"
                || n.Contains("crashhandler") || n.Contains("crashreport") || n == "java" || n == "javaw" || n == "python" || n == "pythonw";
        }

        /// Fichier qui ressemble a un installateur ou un desinstallateur (souvent choisi par erreur a la place du jeu).
        public static bool LooksLikeInstaller(string exe)
        {
            string n = Path.GetFileNameWithoutExtension(exe ?? "").ToLowerInvariant();
            return n.Contains("setup") || n.Contains("install") || n.StartsWith("unins") || n.Contains("uninstall") || n.EndsWith("-updater") || n.Contains("redist");
        }

        public class OpenProgram { public string Title, Path; }

        /// Programmes qui ont une fenetre ouverte (hors Windows et hors cette appli), pour « Ajouter un jeu ».
        public static List<OpenProgram> OpenPrograms()
        {
            var res = new List<OpenProgram>();
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\') + "\\";
            int me = Process.GetCurrentProcess().Id;
            foreach (var w in Proc.VisibleWindows())
            {
                if (w.Key == me) continue;
                string path = Proc.PathOf(w.Key);
                if (path == null || path.StartsWith(win, StringComparison.OrdinalIgnoreCase)) continue;
                if (res.Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
                res.Add(new OpenProgram { Title = w.Value, Path = path });
            }
            return res.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static Game AddManual(string exe)
        {
            var g = new Game { Key = "exe:" + exe.ToLowerInvariant(), Name = NameOf(exe), Source = "Ajouté", Exe = exe, Manual = true, User = 1 };
            lock (sync)
            {
                var all = All;
                var old = all.FirstOrDefault(x => x.Key == g.Key || x.Matches(exe));
                if (old != null) { old.User = 1; g = old; }
                else all.Add(g);
            }
            Save();
            return g;
        }

        public static void Remove(Game g) { lock (sync) { if (list != null) list.Remove(g); } Save(); }

        /// Tous les processus en cours appartenant a ce jeu.
        public static List<int> FindAllRunning(Game g, int exclude)
        {
            var res = new List<int>();
            foreach (var p in Process.GetProcesses())
            {
                int id = p.Id; p.Dispose();
                if (id == exclude || id <= 4) continue;
                if (g.Matches(Proc.PathOf(id))) res.Add(id);
            }
            return res;
        }

        /// Processus en cours appartenant a ce jeu (0 si aucun).
        public static int FindRunning(Game g, int exclude)
        {
            foreach (var p in Process.GetProcesses())
            {
                int id = p.Id; p.Dispose();
                if (id == exclude || id <= 4) continue;
                if (g.Matches(Proc.PathOf(id))) return id;
            }
            return 0;
        }

        // ------------------------------------------------ PCGamingWiki
        /// Nombre de jeux Steam dont l'info HDR n'a pas encore ete cherchee.
        public static int MissingCount { get { return All.Count(x => x.Wiki == null); } }

        /// Cherche l'info HDR des jeux Steam pas encore connus. Renvoie le nombre de jeux mis a jour.
        /// progress(fait, total, jeu en cours) est appele avant chaque jeu ; offline = PCGamingWiki injoignable.
        public static int LookupMissing(Action<int, int, string> progress, out bool offline)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; // TLS 1.2
            offline = false;
            int n = 0;
            // jeux Steam d'abord (identifiant fiable), puis les autres boutiques (recherche par nom exact)
            var todo = All.Where(x => x.Wiki == null).OrderBy(x => x.SteamId == null ? 1 : 0).ToList();
            foreach (var g in todo)
            {
                if (progress != null) progress(n, todo.Count, g.Name);
                string w = WikiHdr(g.SteamId, g.Name);
                if (w == null) { offline = true; break; } // pas de reseau : on reessaiera plus tard
                g.Wiki = w; n++;
                Save();
            }
            if (todo.Count > 0)
                HdrLog.Write(offline
                    ? string.Format("Infos HDR : {0} / {1} jeux consultés, puis arrêt ({2}). Nouvel essai plus tard.", n, todo.Count, LastError)
                    : string.Format("Infos HDR : {0} jeux consultés sur PCGamingWiki.", n));
            return n;
        }

        static string WikiHdr(string appId, string name)
        {
            if (appId == null) return WikiByExactName(name);
            try
            {
                // 1) redirection officielle identifiant Steam -> page du jeu.
                //    Cette adresse est protegee par Cloudflare, qui refuse parfois les applis (403 « challenge ») :
                //    dans ce cas on passe a la recherche par nom (API du wiki, non bloquee) pour le reste de la session.
                string page = null;
                if (!appIdBlocked)
                {
                    try
                    {
                        using (var r = Send("https://www.pcgamingwiki.com/api/appid.php?appid=" + appId, false))
                        {
                            string loc = r.Headers["Location"];
                            int i = loc == null ? -1 : loc.IndexOf("/wiki/", StringComparison.Ordinal);
                            if (i >= 0) page = Uri.UnescapeDataString(loc.Substring(i + 6)).Replace('_', ' ');
                        }
                    }
                    catch (WebException ex)
                    {
                        var resp = ex.Response as HttpWebResponse;
                        if (resp == null || (int)resp.StatusCode != 403) throw;
                        appIdBlocked = true;
                        HdrLog.Write("PCGamingWiki : recherche par identifiant Steam refusée (403), passage à la recherche par nom.");
                    }
                }
                if (page != null) { string w = PageHdr(page, null); if (w != null) return w; }

                // 2) sinon : recherche par nom, en ne gardant QUE la page dont l'identifiant Steam correspond
                //    (evite de confondre "Overwatch" et "Overwatch 2")
                string clean = Regex.Replace(name ?? "", "[™®©]", "").Trim();
                if (clean.Length == 0) return "unknown";
                string json = Get("https://www.pcgamingwiki.com/w/api.php?action=query&list=search&srwhat=title&srlimit=5&format=json&srsearch=" + Uri.EscapeDataString(clean));
                var titles = Regex.Matches(json, "\"title\":\"((?:[^\"\\\\]|\\\\.)*)\"").Cast<Match>().Select(m => Regex.Unescape(m.Groups[1].Value)).ToList();
                // pour menager PCGamingWiki : seulement le titre identique, sinon les premiers resultats
                // (l'identifiant Steam de la page est verifie dans tous les cas)
                string key = TitleKey(clean);
                var check = titles.Where(t => TitleKey(t) == key).ToList();
                if (check.Count == 0) check = titles.Take(appIdBlocked ? 2 : 1).ToList();
                foreach (var t in check)
                {
                    string w = PageHdr(t, appId);
                    if (w != null) return w;
                }
                return "unknown";
            }
            catch (WebException ex)
            {
                LastError = ex.Message;
                var resp = ex.Response as HttpWebResponse;
                return resp != null && (int)resp.StatusCode == 404 ? "unknown" : null;
            }
            catch (Exception ex) { LastError = ex.Message; return null; }
        }

        /// Jeux hors Steam : recherche par nom, en ne gardant qu'un titre IDENTIQUE (sinon « inconnu »,
        /// pour ne pas confondre deux jeux proches comme « Overwatch » et « Overwatch 2 »).
        static string WikiByExactName(string name)
        {
            string want = TitleKey(name);
            if (want.Length == 0) return "unknown";
            try
            {
                string clean = Regex.Replace(name, "[™®©]", "").Trim();
                string json = Get("https://www.pcgamingwiki.com/w/api.php?action=query&list=search&srwhat=title&srlimit=5&format=json&srsearch=" + Uri.EscapeDataString(clean));
                foreach (Match m in Regex.Matches(json, "\"title\":\"((?:[^\"\\\\]|\\\\.)*)\""))
                {
                    string title = Regex.Unescape(m.Groups[1].Value);
                    if (TitleKey(title) != want) continue;
                    string w = PageHdr(title, null);
                    if (w != null) return w;
                }
                return "unknown";
            }
            catch (WebException ex)
            {
                LastError = ex.Message;
                var resp = ex.Response as HttpWebResponse;
                return resp != null && (int)resp.StatusCode == 404 ? "unknown" : null;
            }
            catch (Exception ex) { LastError = ex.Message; return null; }
        }

        /// Titre normalise pour comparaison : minuscules, sans symboles ni ponctuation.
        public static string TitleKey(string s)
        {
            s = Regex.Replace((s ?? "").ToLowerInvariant(), "[™®©]", "");
            s = Regex.Replace(s, "[^\\p{L}\\p{N}]+", " ");
            return s.Trim();
        }

        /// Valeur du champ "hdr" d'une page ; si requiredAppId est donne, la page doit porter cet identifiant Steam.
        static string PageHdr(string title, string requiredAppId)
        {
            string t = Get("https://www.pcgamingwiki.com/w/api.php?action=parse&prop=wikitext&format=json&formatversion=2&redirects=1&page=" + Uri.EscapeDataString(title));
            if (requiredAppId != null)
            {
                var ids = Regex.Match(t, @"\|steam appid\s*=\s*([0-9, ]*)");
                if (!ids.Success || !ids.Groups[1].Value.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Contains(requiredAppId)) return null;
            }
            var m = Regex.Match(t, @"\|hdr\s*=\s*([a-z/ ]*[a-z/])", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value.Trim().ToLowerInvariant();
            return t.Contains("\"wikitext\"") ? "unknown" : null;
        }

        static string Get(string url)
        {
            using (var r = Send(url, true))
            using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
        }

        /// PCGamingWiki limite le debit (erreur 429 « Too Many Requests ») : au plus une requete par seconde,
        /// et en cas de 429 on attend le delai demande (Retry-After, sinon 15 puis 30 s) avant de reessayer.
        const int RequestGapMs = 1000;
        static DateTime nextRequest = DateTime.MinValue;
        static bool appIdBlocked;

        static HttpWebResponse Send(string url, bool redirect)
        {
            for (int attempt = 0; ; attempt++)
            {
                int wait = (int)(nextRequest - DateTime.UtcNow).TotalMilliseconds;
                if (wait > 0) Thread.Sleep(wait);
                nextRequest = DateTime.UtcNow.AddMilliseconds(RequestGapMs);
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = UA; req.AllowAutoRedirect = redirect; req.Timeout = 15000;
                try { return (HttpWebResponse)req.GetResponse(); }
                catch (WebException ex)
                {
                    var r = ex.Response as HttpWebResponse;
                    if (r == null || (int)r.StatusCode != 429 || attempt >= 2) throw;
                    int s;
                    if (!int.TryParse(r.Headers["Retry-After"], out s)) s = 15 << attempt;
                    r.Close();
                    s = Math.Max(5, Math.Min(s, 60));
                    HdrLog.Write(string.Format("PCGamingWiki : trop de requêtes (429), pause de {0} s.", s));
                    Thread.Sleep(s * 1000);
                }
            }
        }
    }

    static class Proc
    {
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder sb, ref int size);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
        delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder s, int max);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);

        /// Fenetres principales visibles (pid, titre) : ni fenetres outils, ni fenetres secondaires.
        public static List<KeyValuePair<int, string>> VisibleWindows()
        {
            var res = new List<KeyValuePair<int, string>>();
            EnumWindows((hwnd, l) =>
            {
                if (!IsWindowVisible(hwnd) || GetWindow(hwnd, 4 /* GW_OWNER */) != IntPtr.Zero) return true;
                if ((GetWindowLong(hwnd, -20 /* GWL_EXSTYLE */) & 0x80 /* WS_EX_TOOLWINDOW */) != 0) return true;
                var sb = new StringBuilder(256);
                if (GetWindowText(hwnd, sb, sb.Capacity) == 0) return true;
                int pid; GetWindowThreadProcessId(hwnd, out pid);
                res.Add(new KeyValuePair<int, string>(pid, sb.ToString()));
                return true;
            }, IntPtr.Zero);
            return res;
        }

        /// Chemin de l'exe d'un processus (droits minimaux, marche aussi pour les jeux proteges).
        public static string PathOf(int pid)
        {
            IntPtr h = OpenProcess(0x1000 /* QUERY_LIMITED_INFORMATION */, false, pid);
            if (h == IntPtr.Zero) return null;
            try
            {
                var sb = new StringBuilder(1024); int size = sb.Capacity;
                return QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString(0, size) : null;
            }
            finally { CloseHandle(h); }
        }
    }

    // ------------------------------------------------------------------ Surveillance des jeux
    /// Journal du HDR auto (%APPDATA%\LNK Screen Manager\hdr-auto.log), ecrit seulement lors d'un evenement.
    static class HdrLog
    {
        static readonly object sync = new object();
        public static void Write(string msg)
        {
            try
            {
                lock (sync)
                {
                    string f = AppData.File("hdr-auto.log");
                    var fi = new FileInfo(f);
                    if (fi.Exists && fi.Length > 200 * 1024) // garde la fin du fichier
                    {
                        var lines = File.ReadAllLines(f, Encoding.UTF8);
                        File.WriteAllLines(f, lines.Skip(lines.Length / 2), Encoding.UTF8);
                    }
                    File.AppendAllText(f, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    class GameWatcher
    {
        delegate void WinEventProc(IntPtr hook, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time);
        delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, WinEventProc proc, uint pid, uint tid, uint flags);
        [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr h);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFOEX mi);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int l, t, r, b; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct MONITORINFOEX { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice; }

        class Session { public Game G; public int Pid; public string Screen; public bool WeTurnedOn; public Process P; }

        /// Apres la fermeture du jeu : delai pour un lanceur qui demarre le vrai jeu, puis delai max
        /// pour un processus du jeu sans fenetre (aide en arriere-plan) avant de couper le HDR.
        const int ExitGraceMs = 4000, HelperGraceMs = 30000;

        readonly TrayApp app;
        readonly WinEventProc proc; // garde la reference (sinon le ramasse-miettes la liberera)
        readonly List<Session> sessions = new List<Session>();
        readonly HashSet<int> ignored = new HashSet<int>();
        readonly int myPid = Process.GetCurrentProcess().Id;
        IntPtr hook;

        static string StateFile { get { return AppData.File("hdr-sessions.txt"); } }

        public GameWatcher(TrayApp app) { this.app = app; proc = OnForeground; }

        public void Start() { if (hook == IntPtr.Zero) hook = SetWinEventHook(3 /* EVENT_SYSTEM_FOREGROUND */, 3, IntPtr.Zero, proc, 0, 0, 0 /* OUTOFCONTEXT */); }
        public void Stop() { if (hook != IntPtr.Zero) { UnhookWinEvent(hook); hook = IntPtr.Zero; } }
        public void Reset() { ignored.Clear(); }
        public int ActiveCount { get { return sessions.Count; } }

        // appele par Windows a chaque changement de fenetre au premier plan (sur le thread de l'interface)
        void OnForeground(IntPtr h, uint ev, IntPtr hwnd, int obj, int child, uint thread, uint time)
        {
            if (hwnd == IntPtr.Zero) return;
            int pid; GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0 || pid == myPid || ignored.Contains(pid) || sessions.Any(s => s.Pid == pid)) return;
            string path = Proc.PathOf(pid);
            var g = path == null ? null : Games.All.FirstOrDefault(x => x.AutoHdr && x.Matches(path));
            if (g == null) { if (ignored.Count > 500) ignored.Clear(); ignored.Add(pid); return; }
            Begin(g, pid, ScreenFor(hwnd), path);
        }

        string ScreenFor(IntPtr hwnd)
        {
            string fixedScreen = Settings.HdrTarget;
            if (!string.IsNullOrEmpty(fixedScreen)) return fixedScreen;
            var mi = new MONITORINFOEX(); mi.cbSize = Marshal.SizeOf(mi);
            if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2 /* NEAREST */), ref mi)) return null;
            var m = Displays.List().FirstOrDefault(x => x.Active && x.GdiName == mi.szDevice);
            return m == null ? null : m.DevicePath;
        }

        static string ScreenName(string path)
        {
            var m = Displays.List().FirstOrDefault(x => x.DevicePath == path);
            return m != null ? m.Name : "?";
        }

        void Begin(Game g, int pid, string screen, string exe)
        {
            var s = new Session { G = g, Pid = pid, Screen = screen };
            if (!Follow(s, pid)) { HdrLog.Write("Jeu ignoré (fin du processus impossible à suivre) : " + g.Name + " pid " + pid + " " + exe); return; }
            sessions.Add(s);
            var mon = Displays.List().FirstOrDefault(m => m.DevicePath == screen);
            if (mon == null || !mon.Active || !mon.HdrSupported)
            {
                HdrLog.Write("Jeu lancé : " + g.Name + " (pid " + pid + ") — écran sans HDR, rien à faire.");
                app.SetStatus(L.T("{0} : l'écran du jeu ne gère pas le HDR.", g.Name), false);
                return;
            }
            if (mon.HdrOn)
            {
                HdrLog.Write("Jeu lancé : " + g.Name + " (pid " + pid + ", " + Path.GetFileName(exe) + ") — HDR déjà actif sur " + mon.Name + ", il ne sera pas coupé à la fermeture.");
                return;
            }
            s.WeTurnedOn = true;
            SaveState();
            HdrLog.Write("Jeu lancé : " + g.Name + " (pid " + pid + ", " + Path.GetFileName(exe) + ") — HDR activé sur " + mon.Name + ".");
            app.HdrAuto(mon, true, L.T("HDR activé pour {0}.", g.Name));
        }

        /// Attend la fin du processus sans sondage (handle du processus surveille par Windows).
        bool Follow(Session s, int pid)
        {
            try
            {
                var p = Process.GetProcessById(pid);
                p.EnableRaisingEvents = true;
                p.Exited += (o, e) => app.Post(() => OnExit(s, p));
                s.Pid = pid; s.P = p;
                try { if (p.HasExited) app.Post(() => OnExit(s, p)); } catch { } // termine entre-temps (lecture parfois refusee : sans gravite)
                return true;
            }
            catch { return false; }
        }

        void OnExit(Session s, Process p)
        {
            if (s.P != p) return; // evenement d'un ancien processus deja remplace
            try { p.Dispose(); } catch { }
            s.P = null;
            HdrLog.Write("Processus fermé : " + s.G.Name + " (pid " + s.Pid + ").");
            After(ExitGraceMs, () => Finish(s, false));
        }

        void After(int ms, Action a)
        {
            var t = new System.Windows.Forms.Timer { Interval = ms };
            t.Tick += (o, e) => { t.Stop(); t.Dispose(); a(); };
            t.Start();
        }

        /// Fenetres visibles des processus donnes.
        static HashSet<int> WithWindow(ICollection<int> pids)
        {
            var res = new HashSet<int>();
            if (pids.Count == 0) return res;
            EnumWindows((hwnd, l) =>
            {
                if (IsWindowVisible(hwnd)) { int pid; GetWindowThreadProcessId(hwnd, out pid); if (pids.Contains(pid)) res.Add(pid); }
                return true;
            }, IntPtr.Zero);
            return res;
        }

        void Finish(Session s, bool helperTimeout)
        {
            if (!sessions.Contains(s) || s.P != null) return;
            var others = Games.FindAllRunning(s.G, s.Pid).Where(id => !sessions.Any(x => x != s && x.Pid == id)).ToList();
            var windowed = WithWindow(others);
            int next = others.FirstOrDefault(id => windowed.Contains(id));
            if (next > 0 && Follow(s, next)) { HdrLog.Write("Le jeu continue : " + s.G.Name + " suivi sur le pid " + next + "."); return; }
            if (others.Count > 0 && !helperTimeout && Follow(s, others[0]))
            {
                // processus du jeu sans fenetre (lanceur en cours de demarrage, ou aide en arriere-plan) :
                // on le suit, mais au plus 30 s s'il n'ouvre pas de fenetre
                HdrLog.Write("Processus du jeu sans fenêtre (pid " + others[0] + ") : attente de " + (HelperGraceMs / 1000) + " s maximum.");
                var followed = s.P;
                After(HelperGraceMs, () =>
                {
                    if (!sessions.Contains(s) || s.P != followed || followed == null) return;
                    if (WithWindow(new[] { s.Pid }).Count > 0) return; // il a ouvert une fenetre : c'est bien le jeu
                    try { followed.Dispose(); } catch { }
                    s.P = null;
                    HdrLog.Write("Aucune fenêtre du jeu après " + (HelperGraceMs / 1000) + " s : fin de la session.");
                    Finish(s, true);
                });
                return;
            }
            End(s);
        }

        void End(Session s)
        {
            sessions.Remove(s);
            if (s.WeTurnedOn)
            {
                var heir = sessions.FirstOrDefault(x => x.Screen == s.Screen);
                if (heir != null) { heir.WeTurnedOn = true; HdrLog.Write("HDR gardé : " + heir.G.Name + " tourne encore sur le même écran."); }
                else
                {
                    var mon = Displays.List().FirstOrDefault(m => m.DevicePath == s.Screen);
                    if (mon != null && mon.Active && mon.HdrOn) { HdrLog.Write("Jeu fermé : " + s.G.Name + " — HDR coupé sur " + mon.Name + "."); app.HdrAuto(mon, false, L.T("HDR coupé : {0} fermé.", s.G.Name)); }
                    else HdrLog.Write("Jeu fermé : " + s.G.Name + " — HDR déjà coupé, rien à faire.");
                }
            }
            else HdrLog.Write("Jeu fermé : " + s.G.Name + " — HDR laissé tel quel (il était déjà actif au lancement).");
            SaveState();
        }

        // ------------------------------------------------ memoire sur disque (survit a un redemarrage de l'appli)
        void SaveState()
        {
            try
            {
                var lines = sessions.Where(x => x.WeTurnedOn).Select(x => x.Screen + "|" + x.G.Key).Distinct().ToList();
                if (lines.Count == 0) { if (File.Exists(StateFile)) File.Delete(StateFile); }
                else File.WriteAllLines(StateFile, lines, Encoding.UTF8);
            }
            catch { }
        }

        /// Au demarrage : reprend les jeux pour lesquels l'appli avait active le HDR avant d'etre fermee.
        public void Recover()
        {
            string[] lines;
            try { if (!File.Exists(StateFile)) return; lines = File.ReadAllLines(StateFile, Encoding.UTF8); }
            catch { return; }
            foreach (var line in lines)
            {
                var p = line.Split('|');
                if (p.Length < 2) continue;
                string screen = p[0], key = p[1];
                var g = Games.All.FirstOrDefault(x => x.Key == key);
                var running = g == null ? new List<int>() : Games.FindAllRunning(g, 0);
                if (g != null && running.Count > 0)
                {
                    var s = new Session { G = g, Screen = screen, WeTurnedOn = true };
                    var windowed = WithWindow(running);
                    int pid = running.FirstOrDefault(id => windowed.Contains(id));
                    if (pid == 0) pid = running[0];
                    if (Follow(s, pid)) { sessions.Add(s); HdrLog.Write("Reprise après redémarrage : " + g.Name + " tourne encore (pid " + pid + "), HDR gardé."); continue; }
                }
                var mon = Displays.List().FirstOrDefault(m => m.DevicePath == screen);
                string name = g != null ? g.Name : key;
                if (mon != null && mon.Active && mon.HdrOn) { HdrLog.Write("Reprise après redémarrage : " + name + " n'est plus lancé — HDR coupé sur " + mon.Name + "."); app.HdrAuto(mon, false, L.T("HDR coupé : {0} fermé.", name)); }
                else HdrLog.Write("Reprise après redémarrage : " + name + " n'est plus lancé, HDR déjà coupé.");
            }
            SaveState();
        }
    }

    // ------------------------------------------------------------------ "Jouer en HDR" (raccourci) : HDR active AVANT le lancement
    static class Player
    {
        public static int Play(string key)
        {
            var g = Games.All.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (g == null) { MessageBox.Show(L.T("Jeu introuvable : {0}", key), Brand.Name); return 1; }

            var mons = Displays.List();
            Monitor target = string.IsNullOrEmpty(Settings.HdrTarget) ? null : mons.FirstOrDefault(m => m.DevicePath == Settings.HdrTarget);
            if (target == null) target = mons.Where(m => m.Active && m.HdrSupported).OrderByDescending(m => m.Primary).FirstOrDefault();
            bool turned = false;
            if (target != null && target.Active && target.HdrSupported && !target.HdrOn) { Displays.SetHdr(target, true); turned = true; Thread.Sleep(500); }
            HdrLog.Write("Jouer en HDR : " + g.Name + " — " + (turned ? "HDR activé sur " + target.Name + " avant le lancement." : "HDR déjà actif ou écran sans HDR, lancement simple."));

            if (g.SteamId != null) Process.Start("steam://rungameid/" + g.SteamId);
            else if (g.EpicApp != null) Process.Start("com.epicgames.launcher://apps/" + Uri.EscapeDataString(g.EpicApp) + "?action=launch&silent=true");
            else Process.Start(new ProcessStartInfo(g.Exe) { WorkingDirectory = Path.GetDirectoryName(g.Exe), UseShellExecute = true });
            if (!turned) return 0;

            Win.Trim();
            // attendre que le jeu demarre (3 min max), puis qu'il se ferme (y compris s'il change d'exe)
            int pid = 0; var until = DateTime.Now.AddMinutes(3);
            while (pid == 0 && DateTime.Now < until) { Thread.Sleep(2000); pid = Games.FindRunning(g, 0); }
            while (pid != 0)
            {
                try { using (var p = Process.GetProcessById(pid)) p.WaitForExit(); } catch { }
                Thread.Sleep(4000);
                pid = Games.FindRunning(g, 0);
            }
            var now = Displays.List().FirstOrDefault(m => m.DevicePath == target.DevicePath);
            if (now != null && now.Active && now.HdrOn) Displays.SetHdr(now, false);
            HdrLog.Write("Jouer en HDR : " + g.Name + " fermé — HDR coupé sur " + target.Name + ".");
            return 0;
        }
    }

}
