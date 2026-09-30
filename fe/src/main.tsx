import React from 'react'
import ReactDOM from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Toaster } from 'sonner'
import App from './App'
import './style.css'
import './clay.css'

ReactDOM.createRoot(document.getElementById('app')!).render(
  <React.StrictMode><QueryClientProvider client={new QueryClient()}><App /><Toaster position="top-right" richColors closeButton /></QueryClientProvider></React.StrictMode>,
)
