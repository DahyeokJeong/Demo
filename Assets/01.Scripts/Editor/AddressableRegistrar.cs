
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

public static class AddressableRegistrar
{
    public static bool Register(string assetPath, string address)
    {
        AddressableAssetSettings settings =
            AddressableAssetSettingsDefaultObject.Settings;

        if (settings == null)
        {
            Debug.LogError("Addressables Settings를 찾을 수 없습니다.");
            return false;
        }

        string guid = AssetDatabase.AssetPathToGUID(assetPath);

        if (string.IsNullOrEmpty(guid))
        {
            Debug.LogError($"에셋 GUID를 찾을 수 없습니다: {assetPath}");
            return false;
        }

        AddressableAssetGroup group = settings.DefaultGroup;

        if (group == null)
        {
            Debug.LogError("Addressables 기본 그룹을 찾을 수 없습니다.");
            return false;
        }

        AddressableAssetEntry entry =
            settings.CreateOrMoveEntry(guid, group);

        if (entry == null)
        {
            Debug.LogError($"Addressables 등록 실패: {assetPath}");
            return false;
        }

        entry.SetAddress(address);

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        Debug.Log($"Addressables 등록 완료: {address}");

        return true;
    }
}
