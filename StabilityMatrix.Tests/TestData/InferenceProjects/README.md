# Saved Inference projects

Project files written by each release, one folder per version, for
`InferenceProjectCorpusTests`. Every file must open in today's Inference tab and queue the same
ComfyUI prompt as its scenario in `InferenceProjectScenarios` applied to a new tab.

Each file is named after its scenario. The input image path is written as `input.png`; the test
points image tabs at its own image after loading.

## Adding a release

On a release that changes what a project stores, write its files from that release's code:

```
SM_WRITE_PROJECT_CORPUS=<repo>/StabilityMatrix.Tests/TestData/InferenceProjects/<version> \
  dotnet test StabilityMatrix.Tests --filter "FullyQualifiedName~InferenceProjectCorpusTests.WriteCorpus"
```

`2.16.5` was written from the `v2.16.5` tag with the generator copied in, its tab classes mapped
to their 2.16.5 names (`InferenceWanTextToVideoViewModel`, `InferenceWanImageToVideoViewModel`,
and `InferenceImageToVideoViewModel` for SVD). Files from older releases are never regenerated.
