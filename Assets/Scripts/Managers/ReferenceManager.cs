using System;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// A static class that allows managers to be registered and looked up by their compile-time type.
/// </summary>
public static class ReferenceManager
{
    #region Variables

    private static readonly Dictionary<Type, object> registeredManagers = new Dictionary<Type, object>();

    #endregion

    #region Initialization

    static ReferenceManager()
    {
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    #endregion

    #region Registration

    /// <summary>
    /// Registers a manager instance by its compile-time type.
    /// </summary>
    /// <typeparam name="T">Manager type.</typeparam>
    /// <param name="managerInstance">Manager instance.</param>
    public static void RegisterManager<T>(T managerInstance) where T : class
    {
        if (managerInstance == null)
        {
            return;
        }

        Type type = typeof(T);
        registeredManagers[type] = managerInstance;
    }

    /// <summary>
    /// Unregisters a manager instance by its compile-time type.
    /// </summary>
    /// <typeparam name="T">Manager type.</typeparam>
    public static void UnregisterManager<T>() where T : class
    {
        Type type = typeof(T);
        if (registeredManagers.ContainsKey(type))
        {
            registeredManagers.Remove(type);
        }
    }

    #endregion

    #region Lookup

    /// <summary>
    /// Returns a registered manager instance or null if it is not present.
    /// </summary>
    /// <typeparam name="T">Manager type.</typeparam>
    /// <returns>Registered manager instance, or null.</returns>
    public static T GetManager<T>() where T : class
    {
        Type type = typeof(T);

        object managerObject;
        if (!registeredManagers.TryGetValue(type, out managerObject))
        {
            return null;
        }

        return managerObject as T;
    }

    /// <summary>
    /// Checks if a manager of the requested type is registered.
    /// </summary>
    /// <typeparam name="T">Manager type.</typeparam>
    /// <returns>True if registered, otherwise false.</returns>
    public static bool Contains<T>() where T : class
    {
        Type type = typeof(T);
        return registeredManagers.ContainsKey(type);
    }

    #endregion

    #region Scene Cleanup

    /// <summary>
    /// Removes UnityEngine.Object-backed entries that were destroyed during scene unload.
    /// </summary>
    /// <param name="scene">Unloaded scene.</param>
    private static void OnSceneUnloaded(Scene scene)
    {
        List<Type> staleKeys = new List<Type>();

        foreach (KeyValuePair<Type, object> pair in registeredManagers)
        {
            UnityEngine.Object unityObject = pair.Value as UnityEngine.Object;
            if (unityObject != null && unityObject == null)
            {
                staleKeys.Add(pair.Key);
            }
        }

        int index = 0;
        while (index < staleKeys.Count)
        {
            registeredManagers.Remove(staleKeys[index]);
            index++;
        }
    }

    #endregion
}