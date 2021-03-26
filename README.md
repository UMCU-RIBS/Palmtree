Palmtree
===============



Build instructions
---------------

With Docker:
- Install docker & docker-compose
- `docker-compose up --build`

Without docker:
- Install .NET Framework 4.8 && msbuild
- `msbuild -m AllProjects.sln property:Configuration=Release`