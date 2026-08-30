using UnityEngine;

public class PlayerVisualSelectorView : MonoBehaviour
{
    [SerializeField] private GameObject[] visualModels;

    public Animator SelectedAnimator { get; private set; }

    private void Awake()
    {
        SelectRandomVisual();
    }

    private void SelectRandomVisual()
    {
        int selectedIndex = Random.Range(0, visualModels.Length);

        for (int i = 0; i < visualModels.Length; i++)
        {
            bool isSelected = i == selectedIndex;

            visualModels[i].SetActive(isSelected);

            if (isSelected)
            {
                SelectedAnimator = visualModels[i].GetComponent<Animator>();
            }
        }
    }
}