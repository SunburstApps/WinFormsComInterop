﻿using System;
using System.Runtime.InteropServices;

namespace MS.Win32
{
    public partial class UnsafeNativeMethods
    {
        [ComImport(), Guid("00000122-0000-0000-C000-000000000046"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IOleDropTarget
        {
            [PreserveSig]
            int OleDragEnter(
                [In, MarshalAs(UnmanagedType.Interface)]
            object pDataObj,
                [In, MarshalAs(UnmanagedType.U4)]
            int grfKeyState,
                [In, MarshalAs(UnmanagedType.U8)]
            long pt,
                [In, Out]
            ref int pdwEffect);

            [PreserveSig]
            int OleDragOver(
                [In, MarshalAs(UnmanagedType.U4)]
            int grfKeyState,
                [In, MarshalAs(UnmanagedType.U8)]
            long pt,
                [In, Out]
            ref int pdwEffect);

            [PreserveSig]
            int OleDragLeave();

            [PreserveSig]
            int OleDrop(
                [In, MarshalAs(UnmanagedType.Interface)]
            object pDataObj,
                [In, MarshalAs(UnmanagedType.U4)]
            int grfKeyState,
                [In, MarshalAs(UnmanagedType.U8)]
            long pt,
                [In, Out]
            ref int pdwEffect);
        }
    }
}
