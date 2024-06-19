#include "Spawner.h"
#include <QGraphicsView>
#include <QGraphicsScene>
#include <QTimer>
#include "PlaySqaure.h"


Spawner::Spawner()
{
    QTimer *Spawn = new QTimer();
  //  connect(Spawn,SIGNAL(timeout(Singleshot())),this,SLOT(Incoming()));
    Spawn->start(2000);
}
