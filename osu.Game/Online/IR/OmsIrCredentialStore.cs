// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace osu.Game.Online.IR
{
    public interface IOmsIrCredentialStore
    {
        OmsIrSession? Read(string target);
        void Write(string target, OmsIrSession session);
        void Delete(string target);
    }

    /// <summary>Generic Windows credentials, scoped to both the OMS save root and IR origin.</summary>
    public sealed class OmsIrCredentialStore : IOmsIrCredentialStore
    {
        private const uint generic_type = 1;
        private const uint local_machine_persistence = 2;
        private const int not_found = 1168;
        private const int maximum_blob_size = 2560;

        public OmsIrSession? Read(string target)
        {
            requireWindows();

            if (!CredRead(target, generic_type, 0, out IntPtr pointer))
            {
                int error = Marshal.GetLastWin32Error();
                if (error == not_found)
                    return null;
                throw new Win32Exception(error, "Windows could not read the OMS IR credential.");
            }

            byte[]? bytes = null;
            try
            {
                NativeCredential credential = Marshal.PtrToStructure<NativeCredential>(pointer);
                if (credential.CredentialBlobSize is 0 or > maximum_blob_size)
                    throw new InvalidDataException("The OMS IR credential has an invalid size.");

                bytes = new byte[(int)credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                return JsonConvert.DeserializeObject<OmsIrSession>(Encoding.UTF8.GetString(bytes))
                       ?? throw new InvalidDataException("The OMS IR credential is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The OMS IR credential cannot be decoded.", exception);
            }
            finally
            {
                if (bytes != null)
                    CryptographicOperations.ZeroMemory(bytes);
                CredFree(pointer);
            }
        }

        public void Write(string target, OmsIrSession session)
        {
            requireWindows();
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(session));
            if (bytes.Length > maximum_blob_size)
            {
                CryptographicOperations.ZeroMemory(bytes);
                throw new InvalidDataException("The OMS IR credential exceeds the Windows credential budget.");
            }

            IntPtr blob = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, blob, bytes.Length);
                var credential = new NativeCredential
                {
                    Type = generic_type,
                    TargetName = target,
                    UserName = session.Username,
                    CredentialBlobSize = (uint)bytes.Length,
                    CredentialBlob = blob,
                    Persist = local_machine_persistence,
                };

                if (!CredWrite(ref credential, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not save the OMS IR credential.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
                Marshal.Copy(bytes, 0, blob, bytes.Length);
                Marshal.FreeHGlobal(blob);
            }
        }

        public void Delete(string target)
        {
            requireWindows();
            if (CredDelete(target, generic_type, 0))
                return;

            int error = Marshal.GetLastWin32Error();
            if (error != not_found)
                throw new Win32Exception(error, "Windows could not remove the OMS IR credential.");
        }

        private static void requireWindows()
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("OMS IR credentials require Windows Credential Manager.");
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeCredential
        {
            public uint Flags;
            public uint Type;
            [MarshalAs(UnmanagedType.LPWStr)] public string? TargetName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
            [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredWrite(ref NativeCredential credential, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("advapi32.dll")]
        private static extern void CredFree(IntPtr credential);
    }
}
