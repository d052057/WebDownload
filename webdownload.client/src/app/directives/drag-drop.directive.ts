import { Directive, EventEmitter, HostBinding, HostListener, Output } from '@angular/core';

@Directive({
  selector: '[appDragDrop]',
})
export class DragDropDirective {
  @Output() fileDropped = new EventEmitter<FileList>();
  @HostBinding('class.drag-over') isDraggingOver = false;

  @HostListener('dragover', ['$event']) onDragOver(evt: DragEvent) {
    evt.preventDefault();
    evt.stopPropagation();
    this.isDraggingOver = true;
  }

  @HostListener('dragleave', ['$event']) onDragLeave(evt: DragEvent) {
    evt.preventDefault();
    evt.stopPropagation();

    // dragleave also fires when the pointer moves onto a child element (icon, text, file
    // input) of the drop zone, which made the highlight flicker. Only clear the highlight
    // when the pointer has really left the zone.
    const host = evt.currentTarget as HTMLElement | null;
    const entering = evt.relatedTarget as Node | null;
    if (host && entering && host.contains(entering)) return;

    this.isDraggingOver = false;
  }

  @HostListener('drop', ['$event']) onDrop(evt: DragEvent) {
    evt.preventDefault();
    evt.stopPropagation();
    this.isDraggingOver = false;

    const files = evt.dataTransfer?.files;
    if (files && files.length > 0) {
      this.fileDropped.emit(files);
    }
  }
}
