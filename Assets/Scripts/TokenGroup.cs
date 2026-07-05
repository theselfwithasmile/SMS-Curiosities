using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TokenGroup : MonoBehaviour
{
    [SerializeField] private GameObject tokenPrefab;
        
    // Start is called before the first frame update
    void Start()
    {
        //spawn dummy token
        GameObject token = Instantiate(tokenPrefab, transform.position, Quaternion.identity);
    }

    // Update is called once per frame
    void Update()
    {

    }
}
