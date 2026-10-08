function copyToClipboard(id, element) {
  const textToCopy = document.getElementById(id).innerText;
  navigator.clipboard.writeText(textToCopy).then(() => {
    const originalText = element.innerText;
    element.innerText = "✓ Copied!";
    element.classList.remove('btn-success');
    element.classList.add('btn-light', 'text-dark');

    setTimeout(() => {
      element.innerText = originalText;
      element.classList.remove('btn-light');
      element.classList.add('btn-success');
    }, 1800);
  }).catch(err => {
    console.error('Copy script execution failed: ', err);
  });
}
