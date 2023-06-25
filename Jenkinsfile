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
                    version = env.TAG_NAME.replaceFirst(/^v/, '') + ".0"
                } else {
                    version = VersionNumber projectStartDate: '2023-06-21', versionNumberString: '1.0.${BUILDS_ALL_TIME}.0', versionPrefix: '', worstResultForIncrement: 'SUCCESS'
                }
            }
        }
        
        stage('Publish') {
            bat "dotnet publish .\\StabilityMatrix\\StabilityMatrix.csproj -c Release -o out -r win-x64 -p:PublishSingleFile=true -p:Version=${version} --self-contained true"
        }
        
        if (env.TAG_NAME) {
            stage('Sentry Release') {
                bat "pip install sentry-cli"
                def sentry_org = "stability-matrix"
                def sentry_project = "dotnet"
                def sentry_environment = "production"
                def sentry_release = "StabilityMatrix@${version}"
                
                bat "sentry-cli releases new -p ${sentry_project} ${sentry_release}"
                bat "sentry-cli releases set-commits ${sentry_release} --auto"
                bat "sentry-cli releases files ${sentry_release} upload-sourcemaps ./out"
                bat "sentry-cli releases finalize ${sentry_release}"
                bat "sentry-cli releases deploys ${sentry_release} new -e ${sentry_environment}"
            }
        }
        
        stage ('Archive Artifacts') {
            archiveArtifacts artifacts: 'out/*.exe', followSymlinks: false
        }
    } else {
        stage('Publish') {
            bat "dotnet publish .\\StabilityMatrix\\StabilityMatrix.csproj -c Release -o out -r win-x64 -p:PublishSingleFile=true --self-contained true"
        }
    }
}
