import { defineConfig } from 'vitepress'

export default defineConfig({
  title: 'Stability Matrix Docs',
  description: 'Documentation for Stability Matrix, a multi-platform package manager for Stable Diffusion and related AI tools.',

  // Keep README.md as the single source of truth for the home page content,
  // while serving it at the site root (index.html) instead of /README.html.
  rewrites: {
    'README.md': 'index.md'
  },

  // Dead-link checking stays ON (default) so a future PR that breaks a
  // relative link fails the build instead of shipping silently.
  ignoreDeadLinks: false,

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
      { text: 'Home', link: '/' },
      { text: 'Getting Started', link: '/getting-started/overview' },
      { text: 'Package Manager', link: '/package-manager/overview' },
      { text: 'Inference', link: '/inference/overview' },
      { text: 'Advanced', link: '/advanced/overview' },
      { text: 'Tips and Tricks', link: '/tips/overview' },
      { text: 'Troubleshooting', link: '/troubleshooting/common-issues' }
    ],

    sidebar: {
      '/getting-started/': [
        {
          text: 'Getting Started',
          items: [
            { text: 'Overview', link: '/getting-started/overview' },
            { text: 'Installation', link: '/getting-started/installation' },
            { text: 'First Launch', link: '/getting-started/first-launch' },
            { text: 'Data Directory', link: '/getting-started/data-directory' }
          ]
        }
      ],
      '/package-manager/': [
        {
          text: 'Package Manager',
          items: [
            { text: 'Overview', link: '/package-manager/overview' },
            { text: 'Supported Packages', link: '/package-manager/supported-packages' },
            { text: 'Installing Packages', link: '/package-manager/installing-packages' }
          ]
        }
      ],
      '/inference/': [
        {
          text: 'Inference',
          items: [
            { text: 'Overview', link: '/inference/overview' }
          ]
        }
      ],
      '/advanced/': [
        {
          text: 'Advanced',
          items: [
            { text: 'Overview', link: '/advanced/overview' },
            { text: 'Hardware Support', link: '/advanced/hardware-support' },
            { text: 'ComfyUI Integration', link: '/advanced/comfyui-integration' },
            { text: 'Environment Variables', link: '/advanced/environment-variables' }
          ]
        }
      ],
      '/tips/': [
        {
          text: 'Tips and Tricks',
          items: [
            { text: 'Overview', link: '/tips/overview' },
            { text: 'Terminology', link: '/tips/terminology' }
          ]
        }
      ],
      '/troubleshooting/': [
        {
          text: 'Troubleshooting',
          items: [
            { text: 'Common Issues', link: '/troubleshooting/common-issues' }
          ]
        }
      ]
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/LykosAI/StabilityMatrix' }
    ]
  }
})
