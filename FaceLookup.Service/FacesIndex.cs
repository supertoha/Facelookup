using FaceLookup.Common;
using FaceLookup.Service.Interfaces;
using HNSW.Net;
using Keras.Models;
using Keras.PreProcessing.Image;
using Numpy;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace FaceLookup.Service
{
    public class FacesIndex<T> where T : IFaceIndexsItem
    {
        public FacesIndex(string face2VectorModelPath, IFacesIndexDataProvider dataProvider)
        {
            _face2VectorModelPath = face2VectorModelPath;
            _dataProvider = dataProvider;
            _distanceFunction = CosineDistance.NonOptimized;
        }

        #region private data

        private readonly IFacesIndexDataProvider _dataProvider;
        private readonly string _face2VectorModelPath;
        private readonly Func<float[], float[], float> _distanceFunction;
        private BaseModel _face2VectorModel;
        private SmallWorld<float[], float> _indexGraph;
        private int _indexVersion;
        private bool _isInited;

        #endregion

        public bool Init()
        {
            if (_isInited)
                return false;

            var parameters = new SmallWorld<float[], float>.Parameters()
            {
                //M = 15,
                //LevelLambda = 1 / Math.Log(15),             
            };
            
            _indexGraph = new SmallWorld<float[], float>(_distanceFunction, DefaultRandomGenerator.Instance, parameters);
            var lastIndexInfo = _dataProvider.GetLastFacesIndex();
            if (lastIndexInfo.indexInfo != null && lastIndexInfo.latentVectors != null)
            {
                using (var ms = new MemoryStream(lastIndexInfo.indexInfo.Data, false))
                    _indexGraph = SmallWorld<float[], float>.DeserializeGraph(lastIndexInfo.latentVectors, _distanceFunction, DefaultRandomGenerator.Instance, ms);
                
                _indexVersion = lastIndexInfo.indexInfo.Version;
            }

            _face2VectorModel = Sequential.LoadModel(_face2VectorModelPath);
            _isInited = true;
            return true;
        }

        public void AddBulkFaces(IEnumerable<T> faces)
        {
            if (_isInited == false)
                throw new Exception("Init method will be called before");

            var butchSize = 100;
            var batches = CollectionHelper.Split(faces, butchSize);

            var collection = new List<float[]>();
            var itemsCollection = new List<IFaceIndexsItem>();
            foreach (var batch in batches)
            {
                var batchImages = batch.Select(info => ReadImage(info.FaceSource)).ToArray();
                var imagesArray = np.array(batchImages);
                var imageLatentVector = _face2VectorModel.Predict(imagesArray);

                for (int i = 0; i < batch.Count; i++)
                {
                    var latentVector = imageLatentVector[i].GetData<float>();
                    var item = batch[i];
                    item.FaceVector = latentVector;

                    itemsCollection.Add(item);

                    collection.Add(latentVector);
                }
            }

            // add to index
            var ids = _indexGraph.AddItems(collection);
            for (int i = 0; i < itemsCollection.Count; i++)
            {
                var item = itemsCollection[i];
                var id = ids[i];
                item.FaceIndexId = id;
            }

            // update db
            using (var ms = new MemoryStream())
            {
                _indexGraph.SerializeGraph(ms);
                ms.Flush();
                _indexVersion = _dataProvider.AddFaces(itemsCollection, ms.ToArray());
            }
                
        }

        public FindFaceInfo[] FindFaces(Bitmap faceImage, int limit=1)
        {
            var imageNdArray = ReadImage(faceImage);
            var imageLatentVector = _face2VectorModel.Predict(np.array(new NDarray[] { imageNdArray }));
            var searchResults = _indexGraph.KNNSearch(imageLatentVector[0].GetData<float>(), limit);

            var resultList = new List<FindFaceInfo>();
            foreach (var result in searchResults)
            {
                var item = _dataProvider.GetFaceByIndexId(result.Id, _indexVersion);

                if (item != null)
                    resultList.Add(new FindFaceInfo { Distance = result.Distance, Face = item });
            }
            return resultList.OrderBy(x => x.Distance).ToArray();
        }

        #region private methods

        private NDarray ReadImage(byte[] data)
        {
            //System.Drawing.Image.FromFile(inputPath)
            using (var ms = new MemoryStream(data, false))
            {
                var image = new Bitmap(ms);
                return ReadImage(image);
            }
            return null;
        }

        private NDarray ReadImage(Bitmap image)
        {
            var resizedImage = ImageHelper.ResizeImage(image, 64, 64);

            var imageData = resizedImage.LockBits(new Rectangle(new Point(), resizedImage.Size), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            var byteArray = new byte[resizedImage.Width * resizedImage.Height * 3];
            Marshal.Copy(imageData.Scan0, byteArray, 0, byteArray.Length);
            var npArray = np.array(byteArray.Select(x => Convert.ToSingle(x)).ToArray());
            var result = npArray.reshape(resizedImage.Width, resizedImage.Height, 3)/255F;

            return result;
        }

        private NDarray ReadImage(string imagePath)
        {
            var image = ImageUtil.LoadImg(imagePath, color_mode: "rgb", target_size: new Keras.Shape(64, 64));
            var imageArray = (ImageUtil.ImageToArray(image) as NDarray) / 255F;
            return imageArray.reshape(64, 64, 3);
        }

        #endregion
    }
}
