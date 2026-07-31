using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Variants;

public class Spawner : MonoBehaviour
{
    [SerializeField] List<Zone> zones;

    // Start is called before the first frame update
    void Start()
    {
        ActivateZone(GameState.Instance.currZoneIdx);
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
        foreach (Zone zone in zones) zone.enabled = false;

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
