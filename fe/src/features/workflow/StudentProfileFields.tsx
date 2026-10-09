export function displayBirth(value?: string | null) { return value ? value.split('-').reverse().join('/') : 'Chưa có' }
export function displayGender(value?: string | null) { return ({ MALE: 'Nam', FEMALE: 'Nữ', OTHER: 'Khác' } as Record<string, string>)[value ?? ''] ?? 'Chưa có' }
export function StudentProfileFields({ birth, gender, onBirth, onGender }: {
  birth: string; gender: string; onBirth: (value: string) => void; onGender: (value: string) => void
}) {
  const today = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date())
  return <div className="workflow-fields">
    <label className="field">Ngày sinh<input type="date" max={today} value={birth} onChange={event => onBirth(event.target.value)} /></label>
    <label className="field">Giới tính<select value={gender} onChange={event => onGender(event.target.value)}><option value="">Chưa có</option><option value="MALE">Nam</option><option value="FEMALE">Nữ</option><option value="OTHER">Khác</option></select></label>
  </div>
}
