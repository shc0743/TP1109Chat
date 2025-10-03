using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation.Collections;

static public class AppUserModel
{
    [DllImport("shell32.dll", SetLastError = true)]
    static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);
    [DllImport("kernel32.dll")]
    static extern IntPtr GetCurrentProcess();
    [DllImport("shell32.dll")]
    static extern int GetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] out string AppID);
    [DllImport("propsys.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern int PropVariantToString(IntPtr pv, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder psz, int cch);
    [DllImport("shell32.dll", SetLastError = true)]
    static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    interface IPropertyStore
    {
        int GetCount(out int count);
        int GetAt(int iProp, out PropertyKey pkey);
        int GetValue(ref PropertyKey key, out PropVariant pv);
        int SetValue(ref PropertyKey key, ref PropVariant pv);
        int Commit();
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr p;
        public int p2;
        public int p3;
    }

    public static readonly string AppId = "com.furieau.apps.TP1109ChatWebApp";

    public static void Initialize()
    {
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppId);
        }
        catch (Exception)
        {
        }
    }
}
