import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'Stability Matrix Docs',
  description: 'Documentation for Stability Matrix, a multi-platform package manager for Stable Diffusion and related AI tools.',

  // Serve everything under /stability-matrix/ from day one so URLs survive the
  // planned move to a multi-product docs repo (SM + Chat + Bench sections) with
  // zero redirects. README.md doubles as the section index. The bare site root
  // 302s here via public/staticwebapp.config.json until a landing page exists.
  rewrites: {
    'README.md': 'stability-matrix/index.md',
    ':path(.*)': 'stability-matrix/:path'
  },

  // Dead-link checking stays ON (default) so a future PR that breaks a
  // relative link fails the build instead of shipping silently.
  ignoreDeadLinks: false,

  markdown: {
    config(md) {
      // README.md is rewritten to the section index, but VitePress emits
      // relative hrefs verbatim — a content link like `./../README` would
      // point at a README.html that doesn't exist in the built site (it
      // renders fine on GitHub and in the in-app viewer, so the content is
      // correct; the mismatch is this site's rewrite). Normalize such links
      // to directory-index form, which resolves everywhere.
      md.core.ruler.push('sm-readme-links', (state) => {
        for (const token of state.tokens) {
          if (token.type !== 'inline' || !token.children) continue
          for (const child of token.children) {
            if (child.type !== 'link_open') continue
            const href = child.attrGet('href')
            const match = href?.match(/^(?:\.\/)?((?:\.\.\/)*)README(?:\.md)?(#.*)?$/)
            if (match) {
              child.attrSet('href', (match[1] || './') + (match[2] ?? ''))
            }
          }
        }
      })
    }
  },

  appearance: 'dark',

  // Requires full git history at build time (fetch-depth: 0 in the deploy job).
  lastUpdated: true,

  sitemap: {
    hostname: 'https://docs.lykos.ai'
  },

  themeConfig: {
    outline: 'deep',

    search: {
      provider: 'local'
    },

    editLink: {
      pattern: 'https://github.com/LykosAI/StabilityMatrix/edit/main/docs/:path',
      text: 'Edit this page on GitHub'
    },

    nav: [
      { text: 'Home', link: '/stability-matrix/' },
      { text: 'Getting Started', link: '/stability-matrix/getting-started/overview' },
      { text: 'Package Manager', link: '/stability-matrix/package-manager/overview' },
      { text: 'Inference', link: '/stability-matrix/inference/overview' },
      { text: 'Advanced', link: '/stability-matrix/advanced/overview' },
      { text: 'Tips and Tricks', link: '/stability-matrix/tips/overview' },
      { text: 'Troubleshooting', link: '/stability-matrix/troubleshooting/common-issues' }
    ],

    sidebar: {
      '/stability-matrix/getting-started/': [
        {
          text: 'Getting Started',
          items: [
            { text: 'Overview', link: '/stability-matrix/getting-started/overview' },
            { text: 'Installation', link: '/stability-matrix/getting-started/installation' },
            { text: 'First Launch', link: '/stability-matrix/getting-started/first-launch' },
            { text: 'Data Directory', link: '/stability-matrix/getting-started/data-directory' }
          ]
        }
      ],
      '/stability-matrix/package-manager/': [
        {
          text: 'Package Manager',
          items: [
            { text: 'Overview', link: '/stability-matrix/package-manager/overview' },
            { text: 'Supported Packages', link: '/stability-matrix/package-manager/supported-packages' },
            { text: 'Installing Packages', link: '/stability-matrix/package-manager/installing-packages' }
          ]
        }
      ],
      '/stability-matrix/inference/': [
        {
          text: 'Inference',
          items: [
            { text: 'Overview', link: '/stability-matrix/inference/overview' }
          ]
        }
      ],
      '/stability-matrix/advanced/': [
        {
          text: 'Advanced',
          items: [
            { text: 'Overview', link: '/stability-matrix/advanced/overview' },
            { text: 'Hardware Support', link: '/stability-matrix/advanced/hardware-support' },
            { text: 'ComfyUI Integration', link: '/stability-matrix/advanced/comfyui-integration' },
            { text: 'Environment Variables', link: '/stability-matrix/advanced/environment-variables' }
          ]
        }
      ],
      '/stability-matrix/tips/': [
        {
          text: 'Tips and Tricks',
          items: [
            { text: 'Overview', link: '/stability-matrix/tips/overview' },
            { text: 'Terminology', link: '/stability-matrix/tips/terminology' }
          ]
        }
      ],
      '/stability-matrix/troubleshooting/': [
        {
          text: 'Troubleshooting',
          items: [
            { text: 'Common Issues', link: '/stability-matrix/troubleshooting/common-issues' }
          ]
        }
      ]
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/LykosAI/StabilityMatrix' }
    ]
  }
})
