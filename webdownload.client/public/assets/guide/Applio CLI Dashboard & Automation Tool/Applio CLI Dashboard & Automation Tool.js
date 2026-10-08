// Attach to window so dynamically injected HTML 'onclick' attributes can find it
window.copyToClipboard = function (id, element) {
  // If element wasn't passed directly, find it via event context or fallback
  const targetElement = element || window.event.target;
  const textToCopy = document.getElementById(id).innerText;

  navigator.clipboard.writeText(textToCopy).then(() => {
    const originalText = targetElement.innerText;
    targetElement.innerText = "✓ Copied!";
    targetElement.classList.remove('btn-success');
    targetElement.classList.add('btn-light', 'text-dark');

    setTimeout(() => {
      targetElement.innerText = originalText;
      targetElement.classList.remove('btn-light', 'text-dark');
      targetElement.classList.add('btn-success');
    }, 1800);
  }).catch(err => {
    console.error('Copy execution script failed: ', err);
  });
}
