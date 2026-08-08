using TMPro;
using UnityEngine;

public sealed class MoveGuideView : MonoBehaviour
{
  [SerializeField]
  private TMP_Text forwardLabel;

  [SerializeField]
  private TMP_Text rightLabel;

  [SerializeField]
  private TMP_Text backLabel;

  [SerializeField]
  private TMP_Text leftLabel;

  [SerializeField]
  private MeshRenderer forwardArrow;

  [SerializeField]
  private MeshRenderer rightArrow;

  [SerializeField]
  private MeshRenderer backArrow;

  [SerializeField]
  private MeshRenderer leftArrow;

  [SerializeField]
  private float visibleSeconds = 2.5f;

  [SerializeField]
  private float fadeOutSeconds = 0.5f;

  private MaterialPropertyBlock propertyBlock;

  public float VisibleSeconds => visibleSeconds;

  public float FadeOutSeconds => fadeOutSeconds;

  private void Awake()
  {
    propertyBlock = new MaterialPropertyBlock();
  }

  public void SetLabels(string forward, string right, string back, string left)
  {
    forwardLabel.text = forward;
    rightLabel.text = right;
    backLabel.text = back;
    leftLabel.text = left;
  }

  public void FaceCamera(Quaternion rotation)
  {
    forwardLabel.transform.rotation = rotation;
    rightLabel.transform.rotation = rotation;
    backLabel.transform.rotation = rotation;
    leftLabel.transform.rotation = rotation;
  }

  public void SetAlpha(float alpha)
  {
    forwardLabel.alpha = alpha;
    rightLabel.alpha = alpha;
    backLabel.alpha = alpha;
    leftLabel.alpha = alpha;

    SetArrowAlpha(forwardArrow, alpha);
    SetArrowAlpha(rightArrow, alpha);
    SetArrowAlpha(backArrow, alpha);
    SetArrowAlpha(leftArrow, alpha);
  }

  private void SetArrowAlpha(MeshRenderer renderer, float alpha)
  {
    renderer.GetPropertyBlock(propertyBlock);
    var color = renderer.sharedMaterial.GetColor("_BaseColor");
    color.a = alpha;
    propertyBlock.SetColor("_BaseColor", color);
    renderer.SetPropertyBlock(propertyBlock);
  }
}
