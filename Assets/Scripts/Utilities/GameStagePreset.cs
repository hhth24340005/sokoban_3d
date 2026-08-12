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

  [SerializeField]
  private GameObject floorPrefab;

  [SerializeField]
  private GameObject wallPrefab;


  public Vector3Int Size => size;

  public Vector3 Center => new(
    (size.x - 1) / 2f,
    (size.y - 1) / 2f,
    (size.z - 1) / 2f
  );

  public IReadOnlyList<Cell> Cells => cells;


  public GameObject FloorPrefab => floorPrefab;

  public GameObject WallPrefab => wallPrefab;

}

[Serializable]
public struct Cell
{
  [SerializeField]
  private List<GameObject> gameObjects;

  public IReadOnlyList<GameObject> GameObjects => gameObjects;

  public Cell(IReadOnlyCollection<GameObject> gameObjects)
  {
    this.gameObjects = new List<GameObject>(gameObjects);
  }
}
