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
                SENTRY_ORG = "stability-matrix"
                SENTRY_PROJECT = "dotnet"
                SENTRY_ENVIRONMENT = "production"
                SENTRY_RELEASE = "StabilityMatrix@${version}"
                
                bat "sentry-cli releases new -p $SENTRY_PROJECT $SENTRY_RELEASE"
                bat "sentry-cli releases set-commits $SENTRY_RELEASE --auto"
                bat "sentry-cli releases files $SENTRY_RELEASE upload-sourcemaps path-to-sourcemaps-if-applicable"
                bat "sentry-cli releases finalize $SENTRY_RELEASE"
                bat "sentry-cli releases deploys $SENTRY_RELEASE new -e $SENTRY_ENVIRONMENT"
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
