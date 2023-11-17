# Palmtree
Palmtree is an open-source framework written in C# for the real-time recording and processing of brain signals in Brain Computer Interface (BCI) research and patient home-use applications.
Development of this software is part of the Utrecht NeuroProsthesis (UNP) project by the RIBS research group of prof. dr. Nick Ramsey at the University Medical Center Utrecht (The Netherlands), with some contributions from external collaborators.

The software architecture (of source modules, filter modules and application modules) is similar to that of BCI2000 (https://www.bci2000.org/), which itself offers an extensive system written in C with many additional modules available.

Palmtree features:
- Input/source modules for devices such as the Medtronic Activa PS+S, Summit RC+S and Blackrock systems
- A high-performance signal processing pipeline that can be adjusted on-the-fly to facilitate different types of BCI control (e.g. continuous or clicks).
- An application layer to allow for 2D or 3D task presentations. Several research tasks are readily available, including:
   - LocalizerTask: simple task allows for the presentation of one or more stimuli (image and/or sound) vs rest
   - CursorTask: continuous control of a cursor on the screen
   - SpellerTask: basic letter-grid that is operated by brain-clicks
   - MoleTask: a "whack-a-mole" like task operated by brain-clicks
- Different levels of (data) logging to be able to safeguard patient privacy
- Matlab & Python scripts for the offline reading of recorded data
- Plugin to capture and integrate readings from motion sensors
- Written in the C# language to allow for both performance and ease-of-development (managed memory etc..)


## Build instructions & Usage
0. Clone the Palmtree repository

### Using Visual studio (recommended)
1. Install Visual Studio
2. Ensure .NET Framework 4.8 SDK and targeting packs are installed, either by:
   - Adding the `.NET Framework 4.8 SDK` and `.NET Framework 4.8 targeting` components in the Visual Studio installation
   - Or, by downloading and installing the .NET Framework 4.8 Developer Pack
3. Open `AllProjects.sln`
4. To run:
   - Either build the entire solution and run one of the executables in the build output folder
   - Or select a specific task project (e.g. `CursorTask`) in the Solution Explorer and set that project starup project (right-click -> 'set as startup project'.). Then run/debug the project (Start button).

### Using MSBuild
1. Install .NET Framework 4.8 and msbuild
2. `msbuild -m AllProjects.sln property:Configuration=Release`

### Using Docker:
1. Install docker and docker-compose
2. `docker-compose up --build`


## Publications
- Leinders, Sacha, et al. "Using fMRI to localize target regions for implanted brain-computer interfaces in locked-in syndrome." Clinical Neurophysiology 155 (2023): 1-15.
- Freudenburg, Zachary, et al. "The dorsolateral pre-frontal cortex bi-polar error-related potential in a locked-in patient implanted with a daily use brain–computer interface." Control Theory and Technology 19.4 (2021): 444-454.
- Leinders, Sacha, et al. "Dorsolateral prefrontal cortex-based control with an implanted brain–computer interface." Scientific Reports 10.1 (2020): 15448.
- Pels, Elmar GM, et al. "Stability of a chronic implanted brain-computer interface in late-stage amyotrophic lateral sclerosis." Clinical Neurophysiology 130.10 (2019): 1798-1803.
- Freudenburg, Zachary V., et al. "Sensorimotor ECoG signal features for BCI control: a comparison between people with locked-in syndrome and able-bodied controls." Frontiers in neuroscience 13 (2019): 1058.
- Vansteensel, Mariska J., et al. "Fully implanted brain–computer interface in a locked-in patient with ALS." New England Journal of Medicine 375.21 (2016): 2060-2066.

## Acknowledgements

- Written by: Max van den Boom (info@maxvandenboom.nl), Benny van der Vijgh (benny@vdvijgh.nl), Erdi Erdal (E.Erdal@umcutrecht.nl), Erik Aarnoutse (E.J.Aarnoutse@umcutrecht.nl), Meron Vermaas
- Filter and task concepts by the UNP Team (neuroprothese@umcutrecht.nl)
- Architecture and several modules (as acknowledged in each header) were adapted from BCI2000 (https://www.bci2000.org/)
- Contributions by: Christopher Coogan, Patrik Andersson

