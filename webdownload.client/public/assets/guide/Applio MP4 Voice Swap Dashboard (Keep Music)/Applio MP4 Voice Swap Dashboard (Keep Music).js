function copyToClipboard(id) {
  const textToCopy = document.getElementById(id).innerText;
  navigator.clipboard.writeText(textToCopy).then(() => {
    const btn = document.querySelector(`[onclick="copyToClipboard('${id}')"]`);
    const originalText = btn.innerText;
    btn.innerText = "Copied!";
    btn.style.backgroundColor = "#4ade80";
    setTimeout(() => {
      btn.innerText = originalText;
      btn.style.backgroundColor = "#3b82f6";
    }, 1500);
  }).catch(err => {
    console.error('Failed to copy text: ', err);
  });
}
