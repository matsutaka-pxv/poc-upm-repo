#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

// Batch mode entry point:
//   Unity.exe -batchmode -projectPath . -executeMethod UniVRM.DevOnly.PackageTgzExporter.ExportAll
// ExportAll is already usable as the batch entry point.
namespace UniVRM.DevOnly {
    public static class PackageTgzExporter {
        private const string UniVrmMenuRoot = "UniVRM";

        private static readonly string[] PackageDirs =
        {
            "Packages/UniGLTF",
            "Packages/VRM",
            "Packages/VRM10",
        };

        // /vrm-c/UniVRM/build/tarballs/*.tgz
        private static readonly string BuildDir =
            Path.Combine(
                Application.dataPath,
                "..",
                "build",
                "tarballs"
            );

        private static Queue<string> _sPendingPackages;
        private static PackRequest   _sCurrentRequest;
        private static string        _sCurrentPackageDir;
        private static string        _sOutputDir;

        [MenuItem(UniVrmMenuRoot + "/Export Package .tgz tarballs")]
        public static void ExportAll()
        {
            _sOutputDir = BuildDir;
            Directory.CreateDirectory(_sOutputDir);

            _sPendingPackages = new Queue<string>(PackageDirs);
            PackNext();
        }

        private static void PackNext()
        {
            while (_sPendingPackages.Count > 0) {
                _sCurrentPackageDir = _sPendingPackages.Dequeue();
                var fullPath = Path.GetFullPath(_sCurrentPackageDir);
                if (! Directory.Exists(fullPath)) {
                    Debug.LogWarning($"[PackageTgzExporter] Package directory not found: {fullPath}");
                    continue;
                }

                Debug.Log($"[PackageTgzExporter] Packing {_sCurrentPackageDir} ...");
                _sCurrentRequest         =  Client.Pack(fullPath, _sOutputDir);
                EditorApplication.update += OnEditorUpdate;
                return;
            }

            // All done
            Debug.Log($"[PackageTgzExporter] All packages exported to: {_sOutputDir}");
            EditorUtility.RevealInFinder(_sOutputDir);
        }

        private static void OnEditorUpdate()
        {
            if (! _sCurrentRequest.IsCompleted) {
                return;
            }

            EditorApplication.update -= OnEditorUpdate;

            if (_sCurrentRequest.Status == StatusCode.Success) {
                Debug.Log($"[PackageTgzExporter] Created: {_sCurrentRequest.Result.tarballPath}");
            } else {
                Debug.LogError(
                    $"[PackageTgzExporter] Failed to pack {_sCurrentPackageDir}: {_sCurrentRequest.Error?.message}"
                );
            }

            PackNext();
        }
    }
}
#endif
