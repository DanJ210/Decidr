import { createApp } from 'vue'
import { createPinia } from 'pinia'
import '@fontsource/instrument-sans/400.css'
import '@fontsource/instrument-sans/500.css'
import '@fontsource/instrument-sans/600.css'
import '@fontsource/instrument-sans/700.css'
import '@fontsource/newsreader/600.css'
import '@fontsource/newsreader/700.css'
import './style.css'
import App from './App.vue'
import { loadAuthenticationConfiguration } from './authConfig'
import router from './router'

async function bootstrap(): Promise<void> {
	await loadAuthenticationConfiguration()

	const app = createApp(App)
	app.use(createPinia())
	app.use(router)
	app.mount('#app')
}

void bootstrap().catch((error: unknown) => {
	console.error(error)
	const root = document.querySelector<HTMLElement>('#app')
	if (root) {
		root.textContent = 'Unable to start Decidr because authentication configuration could not be loaded.'
	}
})
