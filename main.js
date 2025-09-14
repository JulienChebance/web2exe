import markdown from './lib/bs-drawdown.min.js'

fetch('README.md').then((res) => res.text()).then((text) => {
	document.querySelector('main').innerHTML = markdown(text);
});