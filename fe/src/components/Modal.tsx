import { useId, useLayoutEffect, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Toaster } from 'sonner'
import { X } from 'lucide-react'

type Props = { title: string; description?: string; children: ReactNode; onClose: () => void; busy?: boolean; wide?: boolean }

export function Modal({ title, description, children, onClose, busy = false, wide = false }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const backdropPress = useRef(false)
  const titleId = useId()
  const descriptionId = useId()
  useLayoutEffect(() => {
    const dialog = dialogRef.current!
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    dialog.showModal()
    return () => { dialog.close(); document.body.style.overflow = previousOverflow }
  }, [])
  return createPortal(<dialog ref={dialogRef} className={`app-modal ${wide ? 'app-modal-wide' : ''}`}
    aria-labelledby={titleId} aria-describedby={description ? descriptionId : undefined} aria-busy={busy}
    onKeyDown={event => {
      if (event.key === 'Escape') {
        event.preventDefault()
        event.stopPropagation()
        if (!busy) onClose()
      }
    }}
    onCancel={event => { event.preventDefault(); if (!busy) onClose() }}
    onPointerDown={event => { backdropPress.current = event.target === event.currentTarget }}
    onClick={event => {
      const bounds = event.currentTarget.getBoundingClientRect()
      const outside = event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom
      if (backdropPress.current && event.target === event.currentTarget && outside && !busy) onClose()
      backdropPress.current = false
    }}>
    <header className="modal-header"><div><h2 id={titleId}>{title}</h2>{description && <p id={descriptionId}>{description}</p>}</div>
      <button type="button" className="icon-button" aria-label="Đóng cửa sổ" disabled={busy} onClick={onClose}><X size={20} /></button></header>
    <div className="modal-body">{children}</div>
    <Toaster id="edit-modal" position="top-right" richColors closeButton />
  </dialog>, document.body)
}
