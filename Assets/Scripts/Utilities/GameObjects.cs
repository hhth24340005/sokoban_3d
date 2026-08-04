using System;
using UnityEngine;

public readonly struct ScopedGameObject : IDisposable
{
  public GameObject GameObject { get; }

  public ScopedGameObject(GameObject gameObject)
  {
    GameObject = gameObject;
  }

  public void Dispose() => UnityEngine.Object.Destroy(GameObject);

  public static implicit operator GameObject(ScopedGameObject scope) => scope.GameObject;
}

public static class GameObjects
{
  public static ScopedGameObject ChildOf(
    this GameObject parent,
    string name
  )
  {
    var generated = new GameObject(name);
    generated.transform.SetParent(parent.transform, worldPositionStays: false);
    return new ScopedGameObject(generated);
  }

  public static ScopedGameObject ChildOf<C>(
    this GameObject parent,
    C prefab,
    out C component
  ) where C : Component
  {
    var generated = UnityEngine.Object.Instantiate(prefab, parent.transform, false);
    component = generated;
    return new ScopedGameObject(generated.gameObject);
  }

  public static ScopedGameObject ChildOf(
    this GameObject parent,
    GameObject prefab
  )
  {
    var generated = UnityEngine.Object.Instantiate(prefab, parent.transform, false);
    return new ScopedGameObject(generated);
  }

  public static GameObject With<C>(this GameObject self, out C component)
    where C : Component
  {
    component = self.AddComponent<C>();
    return self;
  }

  public static ScopedGameObject With<C>(this ScopedGameObject self, out C component)
    where C : Component
  {
    component = self.GameObject.AddComponent<C>();
    return self;
  }
}
