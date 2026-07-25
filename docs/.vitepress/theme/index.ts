import { h } from 'vue'
import DefaultTheme from 'vitepress/theme'
import LayoutWidthToggle from './LayoutWidthToggle.vue'
import './custom.css'

export default {
  extends: DefaultTheme,
  Layout: () =>
    h(DefaultTheme.Layout, null, {
      'nav-bar-content-after': () => h(LayoutWidthToggle)
    })
}
