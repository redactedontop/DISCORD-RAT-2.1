using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Webcam
{
    public static class Webcam
    {
        public static Dictionary<string, bool> ready = new Dictionary<string, bool>();
        public static Dictionary<string, Bitmap> holder = new Dictionary<string, Bitmap>();
        public static Dictionary<int, string> cameras = new Dictionary<int, string>();
        public static int selected = 1;

        private const int MF_VERSION = 0x00020070;
        private static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE = new Guid("C6E13340-30AC-11d0-A18C-00A0C9118956");
        private static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID = new Guid("73646976-0000-0010-8000-00AA00389B71");

        [DllImport("Mfplat.dll", ExactSpelling = true)] private static extern int MFStartup(int version, int flags);
        [DllImport("Mfplat.dll", ExactSpelling = true)] private static extern int MFShutdown();
        [DllImport("Mf.dll", ExactSpelling = true)] private static extern int MFEnumDeviceSources(IMFAttributes pAttributes, [Out] IMFActivate[] ppSourceActivate, ref int pcSourceActivate);
        [DllImport("Mfreadwrite.dll", ExactSpelling = true)] private static extern int MFCreateSourceReaderFromMediaSource(IMFMediaSource pSource, IMFAttributes pAttributes, out IMFSourceReader ppSourceReader);

        public static string GetWebcams()
        {
            cameras.Clear();
            MFStartup(MF_VERSION, 0);
            Guid attrGuid = MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE;
            MFCreateAttributes(out IMFAttributes attrs, 1);
            attrs.SetGUID(MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID, MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
            int count = 0;
            MFEnumDeviceSources(attrs, null, ref count);
            var activates = new IMFActivate[count];
            MFEnumDeviceSources(attrs, activates, ref count);
            var sb = new StringWriter();
            for (int i = 0; i < count; i++)
            {
                activates[i].GetAllocatedString(MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME, out string name, out _);
                sb.WriteLine($"{i + 1}: {name}");
                cameras[i + 1] = null; // store activate later by index
                holder[$"ACT_{i}"] = null;
                ready[$"ACT_{i}"] = false;
                // For simplicity, store the activate object pointer as string key
                cameras[i + 1] = GCHandle.ToIntPtr(GCHandle.Alloc(activates[i])).ToString();
            }
            return sb.ToString();
        }

        public static bool select(int num)
        {
            if (!cameras.ContainsKey(num)) return false;
            selected = num;
            return true;
        }

        public static void init() => GetWebcams();

        public static byte[] GetImage()
        {
            string key = Path.GetRandomFileName();
            ready[key] = false;
            try
            {
                MFStartup(MF_VERSION, 0);
                var actHandle = GCHandle.FromIntPtr(new IntPtr(long.Parse(cameras[selected])));
                var activate = (IMFActivate)actHandle.Target;
                activate.ActivateObject(typeof(IMFMediaSource).GUID, out object mediaSourceObj);
                var mediaSource = (IMFMediaSource)mediaSourceObj;
                MFCreateSourceReaderFromMediaSource(mediaSource, null, out IMFSourceReader reader);
                reader.SetStreamSelection(0, true);
                reader.ReadSample(0, 0, out int index, out int flags, out long timestamp, out IMFSample sample);
                sample.ConvertToContiguousBuffer(out IMFMediaBuffer buffer);
                buffer.Lock(out IntPtr pBuf, out int maxLen, out int currentLen);
                byte[] data = new byte[currentLen];
                Marshal.Copy(pBuf, data, 0, currentLen);
                buffer.Unlock();
                buffer.Release();
                sample.Release();
                reader.Release();
                mediaSource.Shutdown();
                MFShutdown();
                using var ms = new MemoryStream(data);
                using var bmp = new Bitmap(ms);
                using var outStream = new MemoryStream();
                bmp.Save(outStream, ImageFormat.Jpeg);
                return outStream.ToArray();
            }
            catch
            {
                return Array.Empty<byte>();
            }
            finally
            {
                ready.Remove(key);
            }
        }

        [DllImport("Mfplat.dll", ExactSpelling = true)] private static extern int MFCreateAttributes(out IMFAttributes ppMFAttributes, int cInitialSize);
        private static readonly Guid MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME = new Guid("60ddc253-8ea9-4f10-8e11-1910ea5d2ae1");

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("2CD2D921-C447-44A7-A13C-4ADABFC247E3")]
        private interface IMFAttributes
        {
            int GetItem([In] Guid guidKey, [Out] PropVariant pValue);
            int SetItem([In] Guid guidKey, [In] PropVariant Value);
            int CompareItem([In] Guid guidKey, [In] PropVariant Value, out bool pbResult);
            int Compare([MarshalAs(UnmanagedType.Interface)] IMFAttributes pTheirs, int MatchType, out bool pbResult);
            int GetUINT32([In] Guid guidKey, out int punValue);
            int GetUINT64([In] Guid guidKey, out long punValue);
            int GetDouble([In] Guid guidKey, out double pfValue);
            int GetGUID([In] Guid guidKey, out Guid pguidValue);
            int GetStringLength([In] Guid guidKey, out int pcchLength);
            int GetString([In] Guid guidKey, [Out, MarshalAs(UnmanagedType.LPWStr)] string pwszValue, int cchBufSize, out int pcchLength);
            int GetAllocatedString([In] Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out int pcchLength);
            int GetBlobSize([In] Guid guidKey, out int pcbBlobSize);
            int GetBlob([In] Guid guidKey, [Out] byte[] pBuf, int cbBufSize, out int pcbBlobSize);
            int GetAllocatedBlob([In] Guid guidKey, out IntPtr ip, out int length);
            int GetUnknown([In] Guid guidKey, [In] Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
            int SetUINT32([In] Guid guidKey, int unValue);
            int SetUINT64([In] Guid guidKey, long unValue);
            int SetDouble([In] Guid guidKey, double fValue);
            int SetGUID([In] Guid guidKey, [In] Guid guidValue);
            int SetString([In] Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] string wszValue);
            int SetBlob([In] Guid guidKey, byte[] pBuf, int cbBufSize);
            int SetUnknown([In] Guid guidKey, [MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
            int LockStore();
            int UnlockStore();
            int GetCount(out int pcItems);
            int GetItemByIndex(int unIndex, out Guid pGuidKey, out PropVariant pValue);
            int CopyAllItems([MarshalAs(UnmanagedType.Interface)] IMFAttributes pDest);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("7BE19E73-C9BF-468a-AC5A-A5E8653BEC87")]
        private interface IMFActivate
        {
            int GetItem([In] Guid guidKey, [Out] PropVariant pValue);
            int GetAllocatedString([In] Guid guidKey, [MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out int pcchLength);
            int ActivateObject([In] ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
            // other methods not declared
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("279A808D-AEC7-40C8-9C6B-A6B492C78A66")]
        private interface IMFMediaSource
        {
            void GetCharacteristics();
            void Shutdown();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("70AE66F2-C809-4E4F-8915-BDCB406B7993")]
        private interface IMFSourceReader
        {
            int GetStreamSelection(int dwStreamIndex, out bool pfSelected);
            int SetStreamSelection(int dwStreamIndex, bool fSelected);
            int ReadSample(int dwStreamIndex, int dwControlFlags, out int pdwActualStreamIndex, out int pdwStreamFlags, out long pllTimestamp, out IMFSample ppSample);
            // other methods not declared
            void Release();
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A27003CF-2354-4F2A-8D6A-AB7CFF15437E")]
        private interface IMFSample { void ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer); void Release(); }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("045FA593-8799-42B8-BC8D-8968C6453507")]
        private interface IMFMediaBuffer
        {
            void Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
            void Unlock();
            void GetCurrentLength(out int pcbCurrentLength);
            void Release();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PropVariant { short vt; short wReserved1; short wReserved2; short wReserved3; IntPtr p; int p2; }
    }
}
