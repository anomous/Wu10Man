﻿using Microsoft.Win32;
using System;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;

namespace WereDev.Utils.Wu10Man.Editors
{
    internal static class RegistryEditor
    {
        public static string ReadLocalMachineRegistryValue(string registryKey, string registryName)
        {
            if (string.IsNullOrWhiteSpace(registryKey)) throw new ArgumentNullException(nameof(registryKey));
            if (string.IsNullOrWhiteSpace(registryName)) throw new ArgumentNullException(nameof(registryName));
            using (var regKey = Registry.LocalMachine.OpenSubKey(registryKey))
            {
                var regValue = regKey?.GetValue(registryName);
                return regValue?.ToString();
            }
        }

        public static void WriteLocalMachineRegistryValue(string registryKey, string registryName, string registryValue, RegistryValueKind registryValueKind)
        {
            try
            {
                WriteRegistryValue(Registry.LocalMachine, registryKey, registryName, registryValue, registryValueKind);
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException || ex is SecurityException)
                {
                    TakeOwnership(Registry.LocalMachine, registryKey);
                    SetWritePermission(Registry.LocalMachine, registryKey);
                    WriteRegistryValue(Registry.LocalMachine, registryKey, registryName, registryValue, registryValueKind);
                }
            }
        }

        public static void DeleteLocalMachineRegistryValue(string registryKey, string registryName)
        {
            try
            {
                DeleteRegistryValue(Registry.LocalMachine, registryKey, registryName);
            }
            catch (Exception ex)
            {
                if (ex is UnauthorizedAccessException || ex is SecurityException)
                {
                    TakeOwnership(Registry.LocalMachine, registryKey);
                    SetWritePermission(Registry.LocalMachine, registryKey);
                    DeleteRegistryValue(Registry.LocalMachine, registryKey, registryName);
                }
            }
        }

        private static void WriteRegistryValue(RegistryKey registryRoot, string registryKey, string registryName, string registryValue, RegistryValueKind registryValueKind)
        {
            if (registryRoot == null) throw new ArgumentNullException(nameof(registryRoot));
            if (string.IsNullOrWhiteSpace(registryKey)) throw new ArgumentNullException(nameof(registryKey));
            if (string.IsNullOrWhiteSpace(registryName)) throw new ArgumentNullException(nameof(registryName));

            using (var regKey = OpenOrCreateRegistryKey(registryRoot, registryKey, true))
            {
                regKey.SetValue(registryName, registryValue, registryValueKind);
                regKey.Flush();
            }
        }

        private static RegistryKey OpenOrCreateRegistryKey(RegistryKey registryRoot, string registryKey, bool writable)
        {
            var regKey = registryRoot.OpenSubKey(registryKey, writable);
            if (regKey == null)
                regKey = registryRoot.CreateSubKey(registryKey, writable);
            regKey.Flush();
            return regKey;
        }

        private static void DeleteRegistryValue(RegistryKey registryRoot, string registryKey, string registryName)
        {
            if (registryRoot == null) throw new ArgumentNullException(nameof(registryRoot));
            if (string.IsNullOrWhiteSpace(registryKey)) throw new ArgumentNullException(nameof(registryKey));
            if (string.IsNullOrWhiteSpace(registryName)) throw new ArgumentNullException(nameof(registryName));

            using (var regKey = registryRoot.OpenSubKey(registryKey, false))
            {
                var value = regKey.GetValue(registryName);
                if (value == null) return;
            }

            using (var regKey = registryRoot.OpenSubKey(registryKey, true))
            {
                regKey.DeleteValue(registryName);
            }
        }

        private static void TakeOwnership(RegistryKey registryRoot, string registryKey)
        {
            try
            {
                TokenEditor.AddPrivilege(TokenEditor.SE_RESTORE_NAME);
                TokenEditor.AddPrivilege(TokenEditor.SE_BACKUP_NAME);
                TokenEditor.AddPrivilege(TokenEditor.SE_TAKE_OWNERSHIP_NAME);
                using (var regKey = registryRoot.OpenSubKey(registryKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership))
                {
                    var regSec = regKey.GetAccessControl();
                    regSec.SetOwner(WindowsIdentity.GetCurrent().User);
                    regKey.SetAccessControl(regSec);
                }
            }
            finally
            {
                TokenEditor.RemovePrivilege(TokenEditor.SE_RESTORE_NAME);
                TokenEditor.RemovePrivilege(TokenEditor.SE_BACKUP_NAME);
                TokenEditor.RemovePrivilege(TokenEditor.SE_TAKE_OWNERSHIP_NAME);
            }
        }

        private static void SetWritePermission(RegistryKey registryRoot, string registryKey)
        {
            using (var regKey = registryRoot.OpenSubKey(registryKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions))
            {
                var regSec = regKey.GetAccessControl();

                RegistryAccessRule regRule = new RegistryAccessRule(WindowsIdentity.GetCurrent().User,
                                                                    RegistryRights.FullControl,
                                                                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                                                    PropagationFlags.None,
                                                                    AccessControlType.Allow);

                regSec.AddAccessRule(regRule);
                regKey.SetAccessControl(regSec);
            }
        }
    }
}
