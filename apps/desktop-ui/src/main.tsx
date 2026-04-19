import React from 'react';
import ReactDOM from 'react-dom/client';
import { App } from './pages/App';
import './styles.css';

const savedTheme = window.localStorage.getItem('schemaforge.theme');
const savedLanguage = window.localStorage.getItem('schemaforge.language');

document.documentElement.dataset.theme = savedTheme === 'light' ? 'light' : 'dark';
document.documentElement.lang = savedLanguage === 'pt-BR' ? 'pt-BR' : 'en';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
