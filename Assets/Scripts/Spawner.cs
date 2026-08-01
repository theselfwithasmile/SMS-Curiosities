using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Variants;

public class Spawner : MonoBehaviour
{
    [SerializeField] List<BaseZone> zones;

    // Start is called before the first frame update
    void Start()
    {
        // Gated on GameFlowManager so a fresh load lands on the menu instead of dropping straight
        // into a zone - absent (not yet wired into the scene), falls back to the old always-on
        // behaviour. A reload triggered mid-game (R-key zone cycling, Retry, Next Level) already
        // carries State == Playing across on GameFlowManager's DontDestroyOnLoad instance.
        if (GameFlowManager.Instance == null || GameFlowManager.Instance.State == FlowState.Playing)
        {
            ActivateZone(GameState.Instance.currZoneIdx);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ActivateNextZone();
        }
    }

    void ActivateZone(int idx)
    {
        //disable all zone variants (they share one GameObject)
        foreach (BaseZone zone in zones) zone.enabled = false;

        //enable the requested one
        zones[idx].gameObject.SetActive(true);
        zones[idx].enabled = true;
    }

    void ActivateNextZone()
    {
        GameState.Instance.currZoneIdx = (GameState.Instance.currZoneIdx + 1) % zones.Count;

        //reload scene so the new zone starts from a clean state
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
