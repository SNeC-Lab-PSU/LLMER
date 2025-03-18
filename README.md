# LLMER: Crafting Interactive Extended Reality Worlds with JSON Data Generated by Large Language Models

Welcome to the repository for **LLMER: Crafting Interactive Extended Reality Worlds with JSON Data Generated by Large Language Models**. This repository contains the implementation for the methods and experiments described in our paper.

We are excited to open source our implementation for **academic and non-commercial use**, enabling the community to explore and extend our work.

**Note**: This implementation is currently in a **preliminary stage** and intended for **prototype use only**. Future updates may expand its functionality and robustness.

---

## Prerequisites

- Meta Quest 2/3/Pro (3 is recommended)
- Meta Quest [Space Setup](https://www.meta.com/help/quest/articles/getting-started/getting-started-with-quest-3/suggested-boundary-assisted-space-setup/)
- [Setup Link for App Developement](https://developers.meta.com/horizon/documentation/unity/unity-link/#set-up-link)
- Unity Hub and Unity 2021.3.33f1
- [OpenAI API keys](https://platform.openai.com/docs/guides/production-best-practices/api-keys)
- Python 3.8

---
## Preparation

1. Make sure all [prequisites](#prerequisites) are met.
2. Clone the repository.
3. Replace the placeholder for the OpenAI API key with your own key in the `.env` file. Ensure that quotes are removed here. The `.env` file should look like the following:
    ```
    OPENAI_API_KEY=abc123
    ```
4. Replace server IP and OPENAI_API_KEY in `Assets/Scripts/Utils.cs` with your computer's IP and OpenAI API key. Note: the server IP can be simply `127.0.0.1` in Link mode, while OPENAI_API_KEY here is required only when you want to deploy the application on your headset to run in starndalone mode.
5. Create a virtual environment in Python 3.8 (e.g., [using Anaconda](https://docs.anaconda.com/working-with-conda/environments/#creating-an-environment)) and install the required packages.
    ```bash
    pip install -r requirements.txt
    ```
6. Run `python testAPI.py` in the virtual environment to test the OpenAI API connection.
7. Download Unity Hub and install Unity 2021.3.33f1 in Unity Hub with Android Build Support module.

---
## Steps to Run the Project

1. Run `python ForwardServer.py` in the virtual environment to start the Python server.
2. Open the folder `LLMERQuest` in Unity Hub with Unity 2021.3.33f1. At the first time running the project, you may find a pop-up window asking to restart Unity due to changes of OVRPluging. Click **Restart Editor** to complete the update. You may find some errors in the console, like `[Package Manager Window] Error while getting auth code: User is not logged in or user status invalid.`, which can be ignored.
3. Navigate to `Assets/Scenes` folder, open the `VRWorld` scene for VR mode, or open the `MRWorld` scene for MR mode.
4. Click `Import TMP Essentials` once the TMP Importer window is poped up, restart the Unity Editor once it is imported.
5. Connect the Meta Quest device to the computer, enable Quest Link, and run the project in Unity by clicking the **Play** button.
6. Put on the Meta Quest device, you are supposed to find a blue robot in the environment, interact with it by speaking to control its activities. The audio recording can be triggered by either pressing and holding the right controller's `B` button, or pressing and holding the `R` on the keyboard, or pinching the thumb and middle finger of left hand (only available in the `MRWorld` scene). The audio recording is ended by releasing the hand or the controller's button or the keyboard. An example command for testing is `Create a small red cube`. Note that testing without a Quest device is also supported, in that case, ensure the audio source is properly set in the `AudioInput.cs` script.

---
## Instructions on Adding Local Resources

1. Prepare your own Prefab models. **Note: Colliders are required for the objects to be detected by the Context Library.**
2. Copy or move the Prefabs to the **Assets > Resources** folder.
3. Click **Tools > Collect Resource Names**.
4. Check the flie **Assets > Resources > prefabNames.txt** that your Prefabs are added.
5. You can freely create your models during runtime now. LLMER will automatically use your model once related commands are detected.

---

## Citation
If you use this implementation, methodology, or any part of this repository in your research, please cite our paper:

```bibtex
@article{chen2025llmer,
  title={LLMER: Crafting Interactive Extended Reality Worlds with JSON Data Generated by Large Language Models},
  author={Chen, Jiangong and Wu, Xiaoyi and Lan, Tian and Li, Bin},
  journal={IEEE Transactions on Visualization and Computer Graphics},
  year={2025},
  publisher={IEEE}
}
```

---

## License
This repository is licensed under the **Creative Commons Attribution-NonCommercial 4.0 International (CC BY-NC 4.0) License**. This means:

- You are free to **use, modify, and share** the code and assets **for academic and non-commercial purposes**.
- **Commercial use is prohibited** without explicit permission.

For commercial licensing or inquiries, please **contact us**.

For more details, see the [LICENSE](./LICENSE) file or visit the [official CC BY-NC 4.0 page](https://creativecommons.org/licenses/by-nc/4.0/).

---
## Acknowledgements
Those packages are already included in the source code. You are only required to install them if you want to use the latest version of the packages or meet some issues. Also, the sources are listed here for credit purposes.

First, install [NuGet for Unity](https://github.com/GlitchEnzo/NuGetForUnity).
+ Open Package Manager window (Window | Package Manager)
+ Click + button on the upper-left of a window, and select "Add package from git URL..."
+ Enter the following URL and click Add button
    ```
    https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
    ```


Use plugin of [OpenAI API Wrapper](https://www.nuget.org/packages/OpenAI/).
+ Navigate to NuGet > Manage NuGet Packages
+ Search for OpenAI
+ Install OpenAI wrapper for Unity project

The robot model is from this [Udemy course](https://www.udemy.com/course/multiplayer-virtual-reality-vr-development-with-unity/). Partial of the resources and scripts for the whiteboard set were inspired by this [YouTube video](https://www.youtube.com/watch?v=sHE5ubsP-E8&ab_channel=JustinPBarnett).

---

## Contributions
We welcome academic collaborations and constructive feedback. Feel free to open issues or submit pull requests to enhance this repository post-release. Let’s work together to advance the fields of XR and generative AI!

---

## Contact
For any questions, clarifications, or collaboration opportunities, please reach out via:
- Email: [jiangong@psu.edu]
- GitHub: [JiangongChen](https://github.com/JiangongChen)
- Institution: The Pennsylvania State University

Thank you for your interest in LLMER! Stay tuned for the upcoming release and join us in crafting the future of interactive XR worlds.

