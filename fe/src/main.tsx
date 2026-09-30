import React from 'react'
import ReactDOM from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import App from './App'
import './style.css'

ReactDOM.createRoot(document.getElementById('app')!).render(
  <React.StrictMode><QueryClientProvider client={new QueryClient()}><App /></QueryClientProvider></React.StrictMode>,
)
