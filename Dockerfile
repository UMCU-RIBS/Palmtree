FROM mcr.microsoft.com/dotnet/framework/sdk:4.8

WORKDIR C:\\Palmtree
COPY . .

CMD ["msbuild", "-m", "EmptyProject.sln", "/property:Configuration=Release"]
# CMD ["msbuild", "-m", "AllProjects.sln", "/property:Configuration=Release"]