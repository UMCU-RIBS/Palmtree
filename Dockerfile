FROM mcr.microsoft.com/dotnet/framework/sdk:4.8

WORKDIR C:\\Palmtree
COPY . .

CMD ["msbuild", "-m", "AllProjects.sln", "/property:Configuration=Release"]