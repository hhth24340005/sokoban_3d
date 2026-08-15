using System;
using UnityEngine;

public readonly struct ScopedGameObject : IDisposable
{
  public GameObject GameObject { get; }

  public ScopedGameObject(GameObject gameObject)
  {
    GameObject = gameObject;
  }

  public void Dispose()
  {
    if (GameObject == null)
    {
      return;
    }
    GameObject.SetActive(false);
    UnityEngine.Object.Destroy(GameObject);
  }

  public static implicit operator GameObject(ScopedGameObject scope) => scope.GameObject;
}

public static class GameObjects
{
  public static bool IsPrefab(this GameObject self) => !self.scene.IsValid();

  public static GameObject With<C>(
    this GameObject self,
    out C component,
    bool throwIfPrefab = true,
    bool allowDuplication = false
  )
    where C : Component
  {
    if (throwIfPrefab && self.IsPrefab())
    {
      throw new InvalidOperationException(
        $"Attaching component {typeof(C)} to prefab {self} is not allowed"
      );
    }
    component = null;
    if (!allowDuplication)
    {
      component = self.GetComponent<C>();
    }
    if (component == null)
    {
      component = self.AddComponent<C>();
    }
    return self;
  }

  public static ScopedGameObject With<C>(
    this ScopedGameObject self,
    out C component,
    bool thorwIfPrefab = true,
    bool allowDuplication = false
  ) where C : Component =>
    new(self.GameObject.With(out component, thorwIfPrefab, allowDuplication));

  // ===

  public static ScopedGameObject CreateChild(
    this Transform parent,
    string name
  )
  {
    var generated = new GameObject(name);
    generated.transform.SetParent(parent, worldPositionStays: false);
    return new(generated);
  }

  public static ScopedGameObject CreateChild(
    this GameObject parent,
    string name
  ) => parent.transform.CreateChild(name);

  public static ScopedGameObject CreateChild(
    this ScopedGameObject parent,
    string name
  ) => parent.GameObject.CreateChild(name);

  // ===

  public static ScopedGameObject CreateChild(
    this Transform parent,
    GameObject gameObject,
    bool copyIfExisting = true
  )
  {
    GameObject ret;
    if (copyIfExisting || gameObject.IsPrefab())
    {
      ret = UnityEngine.Object.Instantiate(gameObject, parent, worldPositionStays: false);
    }
    else
    {
      ret = gameObject;
      gameObject.transform.SetParent(parent, worldPositionStays: false);
    }
    return new(ret);
  }

  public static ScopedGameObject CreateChild(
    this GameObject parent,
    GameObject prefab,
    bool copyIfExisting = true
  ) => parent.transform.CreateChild(prefab, copyIfExisting);

  public static ScopedGameObject CreateChild(
    this ScopedGameObject parent,
    GameObject prefab,
    bool copyIfExisting = true
  ) => parent.GameObject.CreateChild(prefab, copyIfExisting);

  // ===

  public static ScopedGameObject CreateChild<C>(
    this Transform parent,
    C gameObject,
    out C component,
    bool copyIfExisting = true
  ) where C : Component =>
    parent
      .CreateChild(gameObject.gameObject, copyIfExisting)
      .With(out component);

  public static ScopedGameObject CreateChild<C>(
    this GameObject parent,
    C gameObject,
    out C component,
    bool copyIfExisting = true
  ) where C : Component => parent.transform.CreateChild(gameObject, out component, copyIfExisting);

  public static ScopedGameObject CreateChild<C>(
    this ScopedGameObject parent,
    C gameObject,
    out C component,
    bool copyIfExisting = true
  ) where C : Component => parent.GameObject.CreateChild(gameObject, out component, copyIfExisting);
}
