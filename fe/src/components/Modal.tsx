import { useId, useLayoutEffect, useRef, type ReactNode } from 'react'
import { createPortal } from 'react-dom'
import { Toaster } from 'sonner'
import { X } from 'lucide-react'

type Props = Readonly<{ title: string; description?: string; children: ReactNode; onClose: () => void; busy?: boolean; wide?: boolean; drawer?: boolean }>

let openDialogs = 0
let originalBodyOverflow = ''

export function Modal({ title, description, children, onClose, busy = false, wide = false, drawer = false }: Props) {
  const dialogRef = useRef<HTMLDialogElement>(null)
  const backdropPress = useRef(false)
  const closeRef = useRef(onClose)
  const busyRef = useRef(busy)
  closeRef.current = onClose
  busyRef.current = busy
  const titleId = useId()
  const descriptionId = useId()
  useLayoutEffect(() => {
    const dialog = dialogRef.current!
    if (openDialogs === 0) originalBodyOverflow = document.body.style.overflow
    openDialogs++
    document.body.style.overflow = 'hidden'
    dialog.showModal()
    const closeOnBackdrop = (event: MouseEvent) => {
      const bounds = dialog.getBoundingClientRect()
      const outside = event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom
      if (backdropPress.current && event.target === dialog && outside && !busyRef.current) closeRef.current()
      backdropPress.current = false
    }
    const trackBackdropPress = (event: PointerEvent) => { backdropPress.current = event.target === dialog }
    dialog.addEventListener('pointerdown', trackBackdropPress)
    dialog.addEventListener('click', closeOnBackdrop)
    return () => {
      dialog.removeEventListener('pointerdown', trackBackdropPress)
      dialog.removeEventListener('click', closeOnBackdrop)
      dialog.close(); openDialogs--; if (openDialogs === 0) document.body.style.overflow = originalBodyOverflow
    }
  }, [])
  return createPortal(<dialog ref={dialogRef} className={`app-modal ${wide ? 'app-modal-wide' : ''} ${drawer ? 'mobile-drawer' : ''}`}
    aria-labelledby={titleId} aria-describedby={description ? descriptionId : undefined} aria-busy={busy}
    onKeyDown={event => {
      if (event.key === 'Escape') {
        event.preventDefault()
        event.stopPropagation()
        if (!busy) onClose()
      }
    }}
    onCancel={event => { event.preventDefault(); if (!busy) onClose() }}
    >
    <header className="modal-header"><div><h2 id={titleId}>{title}</h2>{description && <p id={descriptionId}>{description}</p>}</div>
      <button type="button" className="icon-button" aria-label={drawer ? 'Đóng menu' : 'Đóng cửa sổ'} disabled={busy} onClick={onClose}><X size={20} /></button></header>
    <div className="modal-body">{children}</div>
    <Toaster id="edit-modal" position="top-right" richColors closeButton />
  </dialog>, document.body)
}
