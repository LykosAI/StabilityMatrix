import { readdirSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitepress'

// Release-notes nav derives from the files in docs/release-notes/ so adding a
// page is enough — no second place to update (newest first).
const releaseNotes = readdirSync(
  join(dirname(fileURLToPath(import.meta.url)), '..', 'release-notes')
)
  .filter((f) => /^\d+\.\d+\.\d+\.md$/.test(f))
  .map((f) => f.replace(/\.md$/, ''))
  .sort((a, b) => b.localeCompare(a, undefined, { numeric: true }))

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

  // Apply the saved layout-width preference (nav-bar toggle, see
  // theme/LayoutWidthToggle.vue) before first paint so the page doesn't
  // flash at the wrong width. Full-width is the default.
  head: [
    [
      'script',
      {},
      `(function () { try { if (localStorage.getItem('sm-docs-layout') !== 'centered') document.documentElement.classList.add('sm-fluid') } catch (e) { document.documentElement.classList.add('sm-fluid') } })()`
    ]
  ],

  // Requires full git history at build time (fetch-depth: 0 in the deploy job).
  lastUpdated: true,

  sitemap: {
    hostname: 'https://docs.lykos.ai'
  },

  themeConfig: {
    outline: 'deep',

    // The site-title link defaults to the bare site root, which only resolves
    // via the Azure SWA redirect — point it at the section index directly so
    // it also works in local preview (and needs no redirect hop in prod).
    logoLink: '/stability-matrix/',

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
      { text: 'Troubleshooting', link: '/stability-matrix/troubleshooting/common-issues' },
      { text: 'Release Notes', link: `/stability-matrix/release-notes/${releaseNotes[0]}` }
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
      ],
      '/stability-matrix/release-notes/': [
        {
          text: 'Release Notes',
          items: releaseNotes.map((v) => ({
            text: `v${v}`,
            link: `/stability-matrix/release-notes/${v}`
          }))
        }
      ]
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/LykosAI/StabilityMatrix' }
    ]
  }
})
