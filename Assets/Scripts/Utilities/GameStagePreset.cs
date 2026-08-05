#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/GameStagePreset")]
public sealed class GameStagePreset : ScriptableObject
{
  [SerializeField]
  private Vector3Int size = new(1, 1, 1);

  [SerializeField]
  private Cell[] cells = { };

  public Vector3Int Size => size;

  public IReadOnlyList<Cell> Cells => cells;
}

[Serializable]
public struct Cell
{
  [SerializeField]
  private List<GameObject> gameObjects;

  public IReadOnlyList<GameObject> GameObjects => gameObjects;

  public Cell(IReadOnlyCollection<GameObject> gameObjects)
  {
    var copied = new List<GameObject>(gameObjects.Count);
    foreach (var gameObject in gameObjects)
    {
      copied.Add(gameObject);
    }
    this.gameObjects = copied;
  }
}
