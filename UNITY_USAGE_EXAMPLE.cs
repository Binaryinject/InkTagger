// Unity VKV Usage Example
// This file shows how to correctly read VKV database in Unity
// VKV library: https://github.com/hadashiA/VKV

using System.Text;
using UnityEngine;
using VKV;
using Cysharp.Threading.Tasks;

public class VKVLocalizationExample : MonoBehaviour
{
    private ReadOnlyDatabase database;
    private ReadOnlyTable demoTable;
    
    async UniTask Start()
    {
        // 1. Load the VKV database file (without compression for Unity)
        var vkvPath = System.IO.Path.Combine(Application.streamingAssetsPath, "strings.vkv");
        database = await ReadOnlyDatabase.OpenFileAsync(vkvPath);
        
        // 2. Get a specific table (e.g., "demo", "test", etc.)
        demoTable = database.GetTable("demo");
        
        // 3. Query strings by key
        var text = GetLocalizedString("some_key_id");
        Debug.Log(text);
    }
    
    // Correct way to query: Use STRING key, not byte array
    public string GetLocalizedString(string key)
    {
        try
        {
            // IMPORTANT: Use string key directly, NOT Encoding.UTF8.GetBytes(key)
            // The database was created with KeyEncoding.Ascii, so keys must be strings
            var valueBytes = demoTable.Get(key);
            
            if (valueBytes == null || valueBytes.Length == 0)
            {
                Debug.LogWarning($"Localization key not found: {key}");
                return key;
            }
            
            // Values are stored as UTF-8 bytes
            return Encoding.UTF8.GetString(valueBytes);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error getting localized string for key '{key}': {ex.Message}");
            return key;
        }
    }
    
    void OnDestroy()
    {
        database?.Dispose();
    }
}

/* 
 * COMMON MISTAKES TO AVOID:
 * 
 * ❌ WRONG - This will cause NullReferenceException:
 *    var keyBytes = Encoding.UTF8.GetBytes(key);
 *    var valueBytes = table.Get(keyBytes);
 * 
 * ✅ CORRECT - Use string key:
 *    var valueBytes = table.Get(key);
 * 
 * 
 * GENERATING VKV FOR UNITY:
 * 
 * - Without compression (recommended for Unity):
 *   dotnet run -- --folder=tests --vkv=output --vkv-no-compress
 * 
 * - With compression (requires VKV.Compression in Unity):
 *   dotnet run -- --folder=tests --vkv=output --vkv-compress
 *   Note: Unity project must also install VKV.Compression package
 * 
 * - With table prefix:
 *   dotnet run -- --folder=tests --vkv=output --vkv-table-prefix=loc_
 *   Then access as: database.GetTable("loc_demo")
 */
