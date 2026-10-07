type Option<T extends string> = { value: T; label: string }

export function SectionSwitcher<T extends string>({ label, value, options, onChange }: {
  label: string; value: T; options: Option<T>[]; onChange: (value: T) => void
}) {
  return <div className="section-switcher">
    <label className="field section-select">{label}<select value={value} onChange={event => onChange(event.target.value as T)}>
      {options.map(option => <option key={option.value} value={option.value}>{option.label}</option>)}
    </select></label>
    <div className="list-tabs section-buttons" aria-label={label}>{options.map(option =>
      <button key={option.value} type="button" aria-pressed={value === option.value} onClick={() => onChange(option.value)}>{option.label}</button>)}
    </div>
  </div>
}
