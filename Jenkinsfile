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

    stage('Set Version') {
        if (env.BRANCH_NAME == 'main') {
            version = VersionNumber projectStartDate: '', versionNumberString: '1.0.${BUILDS_ALL_TIME}.0', versionPrefix: '', worstResultForIncrement: 'SUCCESS'
        }
    }

    stage('Publish') {
        if (env.BRANCH_NAME == 'main') {
            bat "dotnet publish .\\StabilityMatrix\\StabilityMatrix.csproj -c Release -o out -r win-x64 -p:PublishSingleFile=true -p:Version=${version} --self-contained true"
        } else {
            bat "dotnet publish .\\StabilityMatrix\\StabilityMatrix.csproj -c Release -o out -r win-x64 -p:PublishSingleFile=true --self-contained true"
        }
    }

    if (env.BRANCH_NAME == "main") {
        stage ('Archive Artifacts') {
            archiveArtifacts artifacts: 'out/*.exe', followSymlinks: false
        }
    }
}
