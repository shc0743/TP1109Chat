import { createApp } from 'vue'
import './style.css'
import App from './App.vue'
import ElementPlus from 'element-plus'
import 'element-plus/dist/index.css'
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
