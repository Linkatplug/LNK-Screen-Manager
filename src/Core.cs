// LNK Screen Manager - coeur : ecrans (API Windows CCD), HDR, disposition memorisee, profils.
// By LNK Co (c) 2026
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;


namespace LnkScreenManager
{
    // ------------------------------------------------------------------ Win32 CCD API
    [StructLayout(LayoutKind.Sequential)]
    public struct LUID { public uint LowPart; public int HighPart;
        public bool Same(LUID o) { return LowPart == o.LowPart && HighPart == o.HighPart; } }

    [StructLayout(LayoutKind.Sequential)]
    public struct PATH_SOURCE_INFO { public LUID adapterId; public uint id; public uint modeInfoIdx; public uint statusFlags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PATH_TARGET_INFO {
        public LUID adapterId; public uint id; public uint modeInfoIdx; public uint outputTechnology;
        public uint rotation; public uint scaling; public uint refreshNum; public uint refreshDen;
        public uint scanLineOrdering; public int targetAvailable; public uint statusFlags; }

    [StructLayout(LayoutKind.Sequential)]
    public struct PATH_INFO { public PATH_SOURCE_INFO sourceInfo; public PATH_TARGET_INFO targetInfo; public uint flags; }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MODE_INFO {
        [FieldOffset(0)] public uint infoType;   // 1 = source, 2 = target, 3 = desktop image
        [FieldOffset(4)] public uint id;
        [FieldOffset(8)] public LUID adapterId;
        [FieldOffset(16)] public uint srcWidth;
        [FieldOffset(20)] public uint srcHeight;
        [FieldOffset(24)] public uint srcPixelFormat;
        [FieldOffset(28)] public int srcX;
        [FieldOffset(32)] public int srcY;
        [FieldOffset(36)] public uint r5; [FieldOffset(40)] public uint r6; [FieldOffset(44)] public uint r7;
        [FieldOffset(48)] public uint r8; [FieldOffset(52)] public uint r9; [FieldOffset(56)] public uint r10;
        [FieldOffset(60)] public uint r11;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEVICE_INFO_HEADER { public uint type; public uint size; public LUID adapterId; public uint id; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TARGET_DEVICE_NAME {
        public DEVICE_INFO_HEADER header; public uint flags; public uint outputTechnology;
        public ushort edidManufactureId; public ushort edidProductCodeId; public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SOURCE_DEVICE_NAME {
        public DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName; }

    [StructLayout(LayoutKind.Sequential)]
    public struct ADVANCED_COLOR_INFO { public DEVICE_INFO_HEADER header; public uint value; public uint colorEncoding; public uint bitsPerColorChannel; }

    [StructLayout(LayoutKind.Sequential)]
    public struct ADVANCED_COLOR_INFO_2 { public DEVICE_INFO_HEADER header; public uint value; public uint colorEncoding; public uint bitsPerColorChannel; public uint activeColorMode; }

    [StructLayout(LayoutKind.Sequential)]
    public struct SET_VALUE { public DEVICE_INFO_HEADER header; public uint value; }

    public static class Native
    {
        public const uint QDC_ALL_PATHS = 0x1, QDC_ONLY_ACTIVE_PATHS = 0x2;
        public const uint SDC_TOPOLOGY_EXTEND = 0x4, SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x20,
            SDC_APPLY = 0x80, SDC_SAVE_TO_DATABASE = 0x200, SDC_ALLOW_CHANGES = 0x400;
        public const uint PATH_ACTIVE = 0x1, MODE_IDX_INVALID = 0xffffffff;

        [DllImport("user32.dll")] public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPaths, out uint numModes);
        [DllImport("user32.dll")] public static extern int QueryDisplayConfig(uint flags, ref uint numPaths, [Out] PATH_INFO[] paths, ref uint numModes, [Out] MODE_INFO[] modes, IntPtr topologyId);
        [DllImport("user32.dll")] public static extern int SetDisplayConfig(uint numPaths, [In] PATH_INFO[] paths, uint numModes, [In] MODE_INFO[] modes, uint flags);
        [DllImport("user32.dll")] public static extern int DisplayConfigGetDeviceInfo(ref TARGET_DEVICE_NAME r);
        [DllImport("user32.dll")] public static extern int DisplayConfigGetDeviceInfo(ref SOURCE_DEVICE_NAME r);
        [DllImport("user32.dll")] public static extern int DisplayConfigGetDeviceInfo(ref ADVANCED_COLOR_INFO r);
        [DllImport("user32.dll")] public static extern int DisplayConfigGetDeviceInfo(ref ADVANCED_COLOR_INFO_2 r);
        [DllImport("user32.dll")] public static extern int DisplayConfigSetDeviceInfo(ref SET_VALUE r);

        public static void Query(uint flags, out PATH_INFO[] paths, out MODE_INFO[] modes)
        {
            for (int attempt = 0; ; attempt++)
            {
                uint np, nm;
                int err = GetDisplayConfigBufferSizes(flags, out np, out nm);
                if (err != 0) throw new Exception("GetDisplayConfigBufferSizes: erreur " + err);
                paths = new PATH_INFO[np]; modes = new MODE_INFO[nm];
                err = QueryDisplayConfig(flags, ref np, paths, ref nm, modes, IntPtr.Zero);
                if (err == 122 && attempt < 5) continue; // ERROR_INSUFFICIENT_BUFFER : la config a change entre-temps
                if (err != 0) throw new Exception("QueryDisplayConfig: erreur " + err);
                Array.Resize(ref paths, (int)np); Array.Resize(ref modes, (int)nm);
                return;
            }
        }
    }

    // ------------------------------------------------------------------ Modele
    public class Monitor
    {
        public string Name, DevicePath, GdiName;
        public string Model; // nom d origine (modele), Name = nom personnalise s il existe
        public bool HasCustomName { get { return Model != null && Model != Name; } }
        public LUID AdapterId; public uint TargetId;
        public bool Active, Primary;
        public int X, Y, Width, Height;
        public bool HdrSupported, HdrOn;
        public int Hz;
        public bool HasModes; public MODE_INFO SrcMode, TgtMode; public PATH_INFO Path;
        public string Label { get {
            string s = Name;
            if (Active) s += "   " + Width + "x" + Height + " @ (" + X + "," + Y + ")" + (Primary ? "  [" + L.T("principal") + "]" : "");
            else s += "   (" + L.T("Éteint") + ")";
            return s; } }
    }

    public static class Displays
    {
        static bool hdr2Api = true; // API Windows 11 24H2+ (HDR distinct de l'ACM), avec repli sur l'ancienne

        public static List<Monitor> List()
        {
            PATH_INFO[] paths; MODE_INFO[] modes;
            Native.Query(Native.QDC_ALL_PATHS, out paths, out modes);
            var result = new List<Monitor>();
            // un chemin actif d'abord, puis les inactifs : on garde un seul chemin par ecran
            foreach (var p in paths.OrderByDescending(pp => (pp.flags & Native.PATH_ACTIVE) != 0))
            {
                if (p.targetInfo.targetAvailable == 0) continue;
                if (result.Any(x => x.AdapterId.Same(p.targetInfo.adapterId) && x.TargetId == p.targetInfo.id)) continue;
                var tn = new TARGET_DEVICE_NAME();
                tn.header.type = 2; tn.header.size = (uint)Marshal.SizeOf(tn);
                tn.header.adapterId = p.targetInfo.adapterId; tn.header.id = p.targetInfo.id;
                if (Native.DisplayConfigGetDeviceInfo(ref tn) != 0) continue;
                if (string.IsNullOrEmpty(tn.monitorDevicePath)) continue;
                var m = new Monitor {
                    Name = string.IsNullOrEmpty(tn.monitorFriendlyDeviceName) ? L.T("Écran inconnu") : tn.monitorFriendlyDeviceName,
                    DevicePath = tn.monitorDevicePath, AdapterId = p.targetInfo.adapterId, TargetId = p.targetInfo.id,
                    Active = (p.flags & Native.PATH_ACTIVE) != 0 };
                if (m.Active)
                {
                    var sn = new SOURCE_DEVICE_NAME();
                    sn.header.type = 1; sn.header.size = (uint)Marshal.SizeOf(sn);
                    sn.header.adapterId = p.sourceInfo.adapterId; sn.header.id = p.sourceInfo.id;
                    if (Native.DisplayConfigGetDeviceInfo(ref sn) == 0) m.GdiName = sn.viewGdiDeviceName;
                    uint mi = p.sourceInfo.modeInfoIdx;
                    if (mi != Native.MODE_IDX_INVALID && mi < modes.Length && modes[mi].infoType == 1)
                    {
                        m.X = modes[mi].srcX; m.Y = modes[mi].srcY;
                        m.Width = (int)modes[mi].srcWidth; m.Height = (int)modes[mi].srcHeight;
                        m.Primary = m.X == 0 && m.Y == 0;
                        uint ti = p.targetInfo.modeInfoIdx;
                        if (ti != Native.MODE_IDX_INVALID && ti < modes.Length && modes[ti].infoType == 2)
                        { m.HasModes = true; m.SrcMode = modes[mi]; m.TgtMode = modes[ti]; m.Path = p; m.Hz = Layout.HzOf(modes[ti]); }
                    }
                    ReadHdr(m);
                }
                result.Add(m);
            }
            // nom unique quand deux ecrans identiques (ex. 2x CQ27G2S)
            foreach (var g in result.GroupBy(m => m.Name).Where(g => g.Count() > 1))
            {
                int i = 1;
                foreach (var m in g.OrderBy(m => Layout.SortX(m)).ThenBy(m => m.DevicePath)) m.Name += " #" + (i++);
            }
            foreach (var m in result) { m.Model = m.Name; string cn; if (Names.TryGet(m.DevicePath, out cn)) m.Name = cn; }
            Layout.Remember(result);
            return result.OrderBy(m => m.Active ? 0 : 1).ThenBy(m => m.X).ThenBy(m => m.Y).ToList();
        }

        static void ReadHdr(Monitor m)
        {
            if (hdr2Api)
            {
                var i2 = new ADVANCED_COLOR_INFO_2();
                i2.header.type = 15; i2.header.size = (uint)Marshal.SizeOf(i2);
                i2.header.adapterId = m.AdapterId; i2.header.id = m.TargetId;
                if (Native.DisplayConfigGetDeviceInfo(ref i2) == 0)
                {
                    m.HdrSupported = (i2.value & 0x10) != 0;   // highDynamicRangeSupported
                    m.HdrOn = (i2.value & 0x20) != 0;          // highDynamicRangeUserEnabled
                    return;
                }
                hdr2Api = false;
            }
            var i = new ADVANCED_COLOR_INFO();
            i.header.type = 9; i.header.size = (uint)Marshal.SizeOf(i);
            i.header.adapterId = m.AdapterId; i.header.id = m.TargetId;
            if (Native.DisplayConfigGetDeviceInfo(ref i) == 0)
            {
                m.HdrSupported = (i.value & 0x1) != 0;
                m.HdrOn = (i.value & 0x2) != 0;
            }
        }

        public static void SetHdr(Monitor m, bool on)
        {
            var s = new SET_VALUE();
            s.header.size = (uint)Marshal.SizeOf(s);
            s.header.adapterId = m.AdapterId; s.header.id = m.TargetId; s.value = on ? 1u : 0u;
            int err = -1;
            if (hdr2Api) { s.header.type = 16; err = Native.DisplayConfigSetDeviceInfo(ref s); } // SET_HDR_STATE
            if (err != 0) { s.header.type = 10; err = Native.DisplayConfigSetDeviceInfo(ref s); } // SET_ADVANCED_COLOR_STATE
            if (err != 0) throw new Exception(L.T("Impossible de changer le HDR de {0} (erreur {1})", m.Name, err));
        }

        static string Key(LUID a, uint id) { return a.LowPart + ":" + a.HighPart + ":" + id; }

        /// Applique en UNE seule operation l'ensemble des ecrans allumes (les autres sont eteints).
        public static void SetActive(ICollection<string> devicePathsOn)
        {
            if (SetActive(devicePathsOn, true) == 0) return;
            int err = SetActive(devicePathsOn, false);
            if (err != 0) throw new Exception(L.T("Windows a refusé la configuration (erreur {0}).", err));
        }

        static int SetActive(ICollection<string> devicePathsOn, bool useSaved)
        {
            if (devicePathsOn.Count == 0) throw new Exception(L.T("Il faut garder au moins un écran allumé."));
            var mons = List();
            var wanted = new HashSet<string>(mons.Where(m => devicePathsOn.Contains(m.DevicePath)).Select(m => Key(m.AdapterId, m.TargetId)));
            if (wanted.Count == 0) throw new Exception(L.T("Aucun des écrans demandés n'est branché."));

            PATH_INFO[] all; MODE_INFO[] modes;
            Native.Query(Native.QDC_ALL_PATHS, out all, out modes);
            var keep = new List<PATH_INFO>();
            var done = new HashSet<string>();
            // 1) chemins deja actifs qu'on garde
            foreach (var p in all)
            {
                string k = Key(p.targetInfo.adapterId, p.targetInfo.id);
                if ((p.flags & Native.PATH_ACTIVE) != 0 && wanted.Contains(k) && done.Add(k)) keep.Add(p);
            }
            // 2) ecrans a rallumer : un chemin inactif avec une source libre
            bool anyEnabled = false;
            var extra = new List<MODE_INFO>();
            foreach (var p0 in all)
            {
                var p = p0;
                string k = Key(p.targetInfo.adapterId, p.targetInfo.id);
                if (!wanted.Contains(k) || done.Contains(k) || p.targetInfo.targetAvailable == 0) continue;
                bool sourceUsed = keep.Any(q => q.sourceInfo.adapterId.Same(p.sourceInfo.adapterId) && q.sourceInfo.id == p.sourceInfo.id);
                if (sourceUsed) continue;
                p.flags = Native.PATH_ACTIVE;
                p.sourceInfo.modeInfoIdx = Native.MODE_IDX_INVALID;
                p.targetInfo.modeInfoIdx = Native.MODE_IDX_INVALID;
                var mon = mons.FirstOrDefault(x => Key(x.AdapterId, x.TargetId) == k);
                Layout.Entry saved;
                if (useSaved && mon != null && Layout.TryGet(mon.DevicePath, out saved))
                {
                    var sm = saved.Src; sm.id = p.sourceInfo.id; sm.adapterId = p.sourceInfo.adapterId;
                    var tm = saved.Tgt; tm.id = p.targetInfo.id; tm.adapterId = p.targetInfo.adapterId;
                    p.sourceInfo.modeInfoIdx = (uint)(modes.Length + extra.Count); extra.Add(sm);
                    p.targetInfo.modeInfoIdx = (uint)(modes.Length + extra.Count); extra.Add(tm);
                    p.targetInfo.refreshNum = saved.Path.targetInfo.refreshNum; p.targetInfo.refreshDen = saved.Path.targetInfo.refreshDen;
                    p.targetInfo.rotation = saved.Path.targetInfo.rotation; p.targetInfo.scaling = saved.Path.targetInfo.scaling;
                    p.targetInfo.scanLineOrdering = saved.Path.targetInfo.scanLineOrdering;
                }
                keep.Add(p); done.Add(k); anyEnabled = true;
            }

            if (extra.Count > 0) modes = modes.Concat(extra).ToArray();
            // 3) on ne garde que les modes references, re-indexes
            var newModes = new List<MODE_INFO>();
            var map = new Dictionary<uint, uint>();
            Func<uint, uint> remap = idx => {
                if (idx == Native.MODE_IDX_INVALID || idx >= modes.Length) return Native.MODE_IDX_INVALID;
                uint n; if (!map.TryGetValue(idx, out n)) { n = (uint)newModes.Count; newModes.Add(modes[idx]); map[idx] = n; }
                return n; };
            var arr = keep.ToArray();
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i].sourceInfo.modeInfoIdx = remap(arr[i].sourceInfo.modeInfoIdx);
                arr[i].targetInfo.modeInfoIdx = remap(arr[i].targetInfo.modeInfoIdx);
            }
            var mArr = newModes.ToArray();

            if (anyEnabled && useSaved)
                for (int i = 0; i < arr.Length; i++)
                {
                    var mon2 = mons.FirstOrDefault(x => x.AdapterId.Same(arr[i].targetInfo.adapterId) && x.TargetId == arr[i].targetInfo.id);
                    Layout.Entry saved2; uint si = arr[i].sourceInfo.modeInfoIdx;
                    if (mon2 != null && si < mArr.Length && Layout.TryGet(mon2.DevicePath, out saved2)) { mArr[si].srcX = saved2.Src.srcX; mArr[si].srcY = saved2.Src.srcY; }
                }
            // 4) si l'ecran principal (0,0) disparait, on decale le bureau pour qu'un ecran restant devienne principal
            var srcIdx = Enumerable.Range(0, mArr.Length).Where(i => mArr[i].infoType == 1).ToList();
            if (srcIdx.Count > 0 && !srcIdx.Any(i => mArr[i].srcX == 0 && mArr[i].srcY == 0))
            {
                int b = srcIdx.OrderBy(i => Math.Abs(mArr[i].srcX) + Math.Abs(mArr[i].srcY)).First();
                int dx = mArr[b].srcX, dy = mArr[b].srcY;
                foreach (int i in srcIdx) { mArr[i].srcX -= dx; mArr[i].srcY -= dy; }
            }

            uint flags = Native.SDC_APPLY | Native.SDC_USE_SUPPLIED_DISPLAY_CONFIG | Native.SDC_ALLOW_CHANGES | Native.SDC_SAVE_TO_DATABASE;
            int err = Native.SetDisplayConfig((uint)arr.Length, arr, (uint)mArr.Length, mArr, flags);
            if (err != 0 && useSaved) return err;
            if (err != 0 && anyEnabled)
            {
                // repli : tout rallumer (Windows reprend les positions memorisees), puis eteindre ceux en trop
                AllOn(); Thread.Sleep(1500);
                var still = List().Where(m => m.Active).Select(m => m.DevicePath).ToList();
                if (still.Any(d => !devicePathsOn.Contains(d))) return SetActive(devicePathsOn, false);
                err = 0;
            }
            return err;
        }

        public static void AllOn()
        {
            int err = Native.SetDisplayConfig(0, null, 0, null, Native.SDC_APPLY | Native.SDC_TOPOLOGY_EXTEND);
            if (err != 0) throw new Exception(L.T("Impossible de tout rallumer (erreur {0}).", err));
        }

        public static void WaitFor(Func<List<Monitor>, bool> cond, int ms = 6000)
        {
            var end = DateTime.Now.AddMilliseconds(ms);
            while (DateTime.Now < end) { Thread.Sleep(300); try { if (cond(List())) { Thread.Sleep(500); return; } } catch { } }
        }
    }

    // ------------------------------------------------------------------ Noms personnalises des ecrans (names.txt)
    public static class Names
    {
        static Dictionary<string, string> cache;
        static string FilePath { get { return AppData.File("names.txt"); } }

        static void Load()
        {
            if (cache != null) return;
            cache = new Dictionary<string, string>();
            try
            {
                if (File.Exists(FilePath))
                    foreach (var l in File.ReadAllLines(FilePath, Encoding.UTF8))
                    {
                        int i = l.IndexOf('|');
                        if (i > 0 && l.Length > i + 1) cache[l.Substring(0, i)] = l.Substring(i + 1);
                    }
            }
            catch { }
        }

        public static bool TryGet(string devicePath, out string name) { Load(); return cache.TryGetValue(devicePath, out name); }

        /// Donne un nom a un ecran ; nom vide = retour au nom d'origine.
        public static void Set(string devicePath, string name)
        {
            Load();
            name = (name ?? "").Replace("|", " ").Replace("\r", " ").Replace("\n", " ").Trim();
            if (name.Length > 40) name = name.Substring(0, 40);
            if (name.Length == 0) cache.Remove(devicePath); else cache[devicePath] = name;
            File.WriteAllLines(FilePath, cache.Select(kv => kv.Key + "|" + kv.Value), Encoding.UTF8);
        }
    }

    // ------------------------------------------------------------------ Dossier de donnees (%APPDATA%\LNK Screen Manager)
    public static class AppData
    {
        public static string Dir
        {
            get
            {
                string d = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LNK Screen Manager");
                Directory.CreateDirectory(d);
                return d;
            }
        }

        /// Chemin d'un fichier de donnees ; reprend automatiquement un ancien fichier pose a cote de l'exe.
        public static string File(string name)
        {
            string p = System.IO.Path.Combine(Dir, name);
            if (!System.IO.File.Exists(p))
            {
                try
                {
                    string old = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(AppData).Assembly.Location), name);
                    if (System.IO.File.Exists(old)) System.IO.File.Copy(old, p);
                }
                catch { }
            }
            return p;
        }
    }

    // ------------------------------------------------------------------ Disposition memorisee (position, resolution, frequence)
    public static class Layout
    {
        public class Entry { public MODE_INFO Src, Tgt; public PATH_INFO Path; }
        static Dictionary<string, Entry> cache;
        static string FilePath { get { return AppData.File("layout.txt"); } }

        static string Enc<T>(T s) where T : struct { int n = Marshal.SizeOf(typeof(T)); var b = new byte[n]; var h = GCHandle.Alloc(b, GCHandleType.Pinned); Marshal.StructureToPtr(s, h.AddrOfPinnedObject(), false); h.Free(); return Convert.ToBase64String(b); }
        static T Dec<T>(string s) where T : struct { var b = Convert.FromBase64String(s); var h = GCHandle.Alloc(b, GCHandleType.Pinned); var r = (T)Marshal.PtrToStructure(h.AddrOfPinnedObject(), typeof(T)); h.Free(); return r; }

        static void Load()
        {
            if (cache != null) return;
            cache = new Dictionary<string, Entry>();
            try { if (File.Exists(FilePath)) foreach (var l in File.ReadAllLines(FilePath)) { var p = l.Split('|'); if (p.Length == 4) cache[p[0]] = new Entry { Src = Dec<MODE_INFO>(p[1]), Tgt = Dec<MODE_INFO>(p[2]), Path = Dec<PATH_INFO>(p[3]) }; } } catch { }
        }

        public static bool TryGet(string devicePath, out Entry e) { Load(); return cache.TryGetValue(devicePath, out e); }

        // comparaison directe des 64 octets utiles du mode (sans allocation)
        static bool Same(MODE_INFO a, MODE_INFO b)
        {
            return a.infoType == b.infoType && a.srcWidth == b.srcWidth && a.srcHeight == b.srcHeight && a.srcPixelFormat == b.srcPixelFormat
                && a.srcX == b.srcX && a.srcY == b.srcY && a.r5 == b.r5 && a.r6 == b.r6 && a.r7 == b.r7
                && a.r8 == b.r8 && a.r9 == b.r9 && a.r10 == b.r10 && a.r11 == b.r11;
        }

        // Frequence (Hz) d'un mode cible : vSyncFreq = offsets 32/36 de l'union DISPLAYCONFIG_VIDEO_SIGNAL_INFO
        public static int HzOf(MODE_INFO t)
        {
            uint num = (uint)t.srcY, den = t.r5;
            return den == 0 ? 0 : (int)Math.Round((double)num / den);
        }

        public static int SortX(Monitor m) { Entry e; return TryGet(m.DevicePath, out e) ? e.Src.srcX : m.X; }

        /// Rectangle de bureau (memorise si l'ecran est eteint) + infos d'affichage.
        public static bool Bounds(Monitor m, out Rectangle r, out int hz)
        {
            Entry e;
            if (TryGet(m.DevicePath, out e)) { r = new Rectangle(e.Src.srcX, e.Src.srcY, (int)e.Src.srcWidth, (int)e.Src.srcHeight); hz = HzOf(e.Tgt); if (!m.Active) return true; }
            else { r = Rectangle.Empty; hz = 0; }
            if (m.Active) { r = new Rectangle(m.X, m.Y, m.Width, m.Height); hz = m.Hz; return true; }
            return false;
        }

        // Memorise la disposition quand tous les ecrans sont allumes (= disposition "normale"), ou la 1re fois qu'on voit un ecran.
        public static void Remember(List<Monitor> mons)
        {
            Load();
            bool allOn = mons.All(m => m.Active);
            bool changed = false;
            foreach (var m in mons.Where(m => m.HasModes && (allOn || !cache.ContainsKey(m.DevicePath))))
            {
                var e = new Entry { Src = m.SrcMode, Tgt = m.TgtMode, Path = m.Path };
                Entry old;
                if (!cache.TryGetValue(m.DevicePath, out old) || !Same(old.Src, e.Src) || !Same(old.Tgt, e.Tgt)) { cache[m.DevicePath] = e; changed = true; }
            }
            if (changed) try { File.WriteAllLines(FilePath, cache.Select(kv => kv.Key + "|" + Enc(kv.Value.Src) + "|" + Enc(kv.Value.Tgt) + "|" + Enc(kv.Value.Path))); } catch { }
        }
    }

    // ------------------------------------------------------------------ Profils (fichier texte a cote de l'exe)
    public class Profile { public string Name; public Dictionary<string, bool[]> Screens = new Dictionary<string, bool[]>(); } // [allume, hdr]

    public static class Profiles
    {
        public static string FilePath { get { return AppData.File("profiles.txt"); } }

        public static List<Profile> Load()
        {
            var list = new List<Profile>();
            if (!File.Exists(FilePath)) return list;
            foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                var parts = line.Split('|');
                if (parts.Length < 2 || parts[0].Trim() == "" || parts[0].StartsWith("#")) continue;
                var p = new Profile { Name = parts[0].Trim() };
                foreach (var s in parts[1].Split(';'))
                {
                    int eq = s.LastIndexOf('='); if (eq < 0) continue;
                    var v = s.Substring(eq + 1).Split(',');
                    p.Screens[s.Substring(0, eq)] = new[] { v[0] == "1", v.Length > 1 && v[1] == "1" };
                }
                list.Add(p);
            }
            return list;
        }

        public static void Save(List<Profile> list)
        {
            File.WriteAllLines(FilePath, list.Select(p => p.Name + "|" + string.Join(";",
                p.Screens.Select(kv => kv.Key + "=" + (kv.Value[0] ? "1" : "0") + "," + (kv.Value[1] ? "1" : "0")))), Encoding.UTF8);
        }

        public static Profile FromCurrent(string name)
        {
            var p = new Profile { Name = name };
            foreach (var m in Displays.List()) p.Screens[m.DevicePath] = new[] { m.Active, m.HdrOn };
            return p;
        }

        public static void Apply(Profile p)
        {
            var on = p.Screens.Where(kv => kv.Value[0]).Select(kv => kv.Key).ToList();
            var current = Displays.List();
            var currentOn = current.Where(m => m.Active).Select(m => m.DevicePath).ToList();
            bool topoChange = !(new HashSet<string>(currentOn).SetEquals(on.Where(d => current.Any(m => m.DevicePath == d))));
            if (topoChange)
            {
                Displays.SetActive(on);
                Displays.WaitFor(l => on.Where(d => l.Any(m => m.DevicePath == d)).All(d => l.Any(m => m.DevicePath == d && m.Active)));
            }
            foreach (var m in Displays.List().Where(m => m.Active && m.HdrSupported))
            {
                bool[] v;
                if (p.Screens.TryGetValue(m.DevicePath, out v) && v[1] != m.HdrOn) Displays.SetHdr(m, v[1]);
            }
        }
    }

}
