FROM mcr.microsoft.com/dotnet/framework/sdk:4.8
COPY . .

RUN msbuild AllProjects.sln