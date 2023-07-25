node("Windows") {
    def repoName = "StabilityMatrix"
    def author = "ionite34"
    def version = ""

    stage('Clean') {
        deleteDir()
    }

    stage('Checkout') {
        git branch: env.BRANCH_NAME, credentialsId: 'Ionite', url: "https://github.com/${author}/${repoName}.git"
    }
    
    stage('Test') {
        bat "dotnet test StabilityMatrix.Tests"
    }

    if (env.BRANCH_NAME == 'main') {
    
        stage('Set Version') {
            script {
                if (env.TAG_NAME) {
                    version = env.TAG_NAME.replaceFirst(/^v/, '')
                } else {
                    version = VersionNumber projectStartDate: '2023-06-21', versionNumberString: '${BUILDS_ALL_TIME}', worstResultForIncrement: 'SUCCESS'
                }
            }
        }
        
        stage('Publish') {
            bat "dotnet publish .\\StabilityMatrix.Avalonia\\StabilityMatrix.Avalonia.csproj -c Release -o out -r win-x64 -p:PublishSingleFile=true -p:VersionPrefix=2.0.0 -p:VersionSuffix=${version} -p:IncludeNativeLibrariesForSelfExtract=true"
        }
        
        stage ('Archive Artifacts') {
            archiveArtifacts artifacts: 'out/*.exe', followSymlinks: false
        }
    } else {
        stage('Publish') {
            bat "dotnet publish .\\StabilityMatrix.Avalonia\\StabilityMatrix.Avalonia.csproj -c Release -o out -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true"
        }
    }
}
