import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import './polyfill/index.js'
import ElementPlus from 'element-plus'
// import ContextMenu from '@imengyu/vue3-context-menu' // 可访问性不佳，不再使用（键盘导航不支持）
import 'element-plus/dist/index.css'
// import '@imengyu/vue3-context-menu/lib/vue3-context-menu.css'
import File from './file'

{
    const profile = sessionStorage.getItem("profile");
    if (profile !== null) File.setProfile(profile);
}

createApp(App).use(ElementPlus).mount('#app')

window.addEventListener('keydown', e => {
    if (e.key.toUpperCase() === 'W' && e.ctrlKey) {
        e.preventDefault();
        window.close();
    }
});
