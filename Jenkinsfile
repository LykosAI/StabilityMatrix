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

    stage('Build') {
        bat "dotnet publish -c Release -o out -r win-x64 --self-contained true"
    }

    stage('Set Version') {
        version = VersionNumber projectStartDate: '', versionNumberString: '${BUILD_DATE_FORMATTED, "yy"}.${BUILD_WEEK}.${BUILDS_THIS_WEEK}', versionPrefix: '', worstResultForIncrement: 'SUCCESS'
    }

    stage ('Archive Artifacts') {
        archiveArtifacts artifacts: 'out/**/*.*', followSymlinks: false
    }
}
