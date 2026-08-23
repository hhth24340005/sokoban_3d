using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "StageSymbolTable", menuName = "Game/StageSymbolTable")]
public sealed class StageSymbolTable : ScriptableObject
{
  [SerializeField]
  private char emptySymbol = '.';

  [SerializeField]
  private List<SymbolBinding> bindings;

  private IReadOnlyDictionary<char, StageObject> _cache;

  public char EmptySymbol => emptySymbol;

  public IReadOnlyDictionary<char, StageObject> Table => _cache ??= Build();

  private IReadOnlyDictionary<char, StageObject> Build()
  {
    var table = new Dictionary<char, StageObject>();
    foreach (
      var binding in
        bindings.Where(binding => !table.TryAdd(binding.Symbol, binding.Prefab))
    )
    {
      Debug.LogError($"Duplicate symbol: {binding.Symbol}");
    }
    return table;
  }

  private void OnValidate() => _cache = null;
}

[Serializable]
public struct SymbolBinding
{
  [SerializeField]
  private char symbol;

  [SerializeField]
  private StageObject prefab;

  public char Symbol => symbol;

  public StageObject Prefab => prefab;
}