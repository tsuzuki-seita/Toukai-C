using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TitleSceneController : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
    
    public void OnStartButtonClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Main");
    }
}
